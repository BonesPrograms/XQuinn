using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Reflection;
using XQuinn.LangInterp.SyntaxTree;
using System.Linq;
using System;
using System.Collections.Generic;
using XQuinn.LangInterp;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.Runtime
{


    internal sealed class NavigatorCore
    {


        internal TypeBook? LocalCache { get; set; }
        CallLexer Lexer { get; init; } = new();
        internal Parser Parser { get; private init; }
        internal Reflector Reflector { get; private init; }
        internal Invoker Invoker { get; private init; }
        internal object? Object { get; private set; }
        internal string? Reference { get; private set; }
        internal Type? ObjectType => Object?.GetType();
        internal Type? StaticType { get; private set; }
        internal TypeString? ImplicitRef { get; private set; }
        internal Dictionary<MethodKey, MethodBase> Methods { get; private init; } = new();
        internal Dictionary<string, FieldInfo> Fields { get; private init; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, PropertyInfo> Props { get; private init; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, Reference> References { get; private init; } = new(StringComparer.OrdinalIgnoreCase);

        internal const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        public NavigatorCore()
        {
            Parser = new(this);
            Reflector = new(this);
            Invoker = new(this);
        }
        internal object? Instruct(string invocation)
        {
            char controller = default;
            int substring = -1;
            for (int i = 0; i < invocation.Length; i++)
            {
                if (invocation[i] != ' ')
                {
                    controller = invocation[i];
                    substring = i + 1;
                    break;
                }
            }
            if (substring == -1)
                return new EmptyInstruction();
            return controller switch
            {
                '+' => NewRef(invocation.Substring(substring)),
                '-' => RemoveRef(invocation.Substring(substring)),
                '$' => LoadStatic(invocation.Substring(substring)),
                '#' or '*' => LoadObject(invocation.Substring(substring)),
                '^' => Cast(invocation.Substring(substring)),
                '?' => Query(invocation.Substring(substring)),
                _ => ReturnOrAssign(invocation)
            };
        }

        IEnumerable<string> Query(string invocation)
        {
            invocation = invocation.Trim();
            if (invocation.EqualsCaseless("vars") || invocation.EqualsCaseless("refs"))
                return References.Select(x => $"Key: {x.Key} :: {x.Value}");
            if (StaticType == null)
                throw new InvalidOperationException("No loaded type to query. Use the static methods Fields/Methods/Props in class Types to query unloaded types.");
            if (SimpleLex.ImplicitThisMethodCall(invocation))
            {
                MethodString query = Lexer.MethodTemplate(invocation, ImplicitRef!, ImplicitRef);
                if (query.Name.EqualsCaseless("methods"))
                    return ParseQuery(Methods, query);
                if (query.Name.EqualsCaseless("fields"))
                    return ParseQuery(Fields, query);
                if (query.Name.EqualsCaseless("props"))
                    return ParseQuery(Props, query);
            }
            else
            {
                if (invocation.EqualsCaseless("methods"))
                    return Types.ReadMembers(Methods, null, Flag);
                if (invocation.EqualsCaseless("fields"))
                    return Types.ReadMembers(Fields, null, Flag);
                if (invocation.EqualsCaseless("props"))
                    return Types.ReadMembers(Props, null, Flag);
            }
            throw new ArgumentException("Invalid query");
        }

        IEnumerable<string> ParseQuery<K, V>(Dictionary<K, V> dic, MethodString query) where V : MemberInfo where K : notnull
        {
            if (query.Params.Count <= 2)
            {
                string? contains = query.Params.Count >= 1 ? (string?)Parser.NewArg(query.Params[0], typeof(string)) : null;
                BindingFlags flags = query.Params.Count == 2 ? (BindingFlags)Parser.NewArg(query.Params[1], typeof(BindingFlags))! : Flag;
                return Types.ReadMembers(dic, contains, flags);
            }
            throw new TargetParameterCountException("Local query is max 2 params: a containig string and bindingflags search flags.");
        }

        public Type LoadStatic(string typeName)
        {
            TypeString tstring = TypeString.New(typeName.Trim());
            Type t = tstring.ToType();
            ImplicitRef = tstring;
            LoadTypeMembers(t);
            Object = null;
            Reference = null;
            return t;
        }

        object LoadObject(string invocation)
        {
            invocation = invocation.Trim();
            object? instance = null;
            bool referenced = false;
            if (References.TryGetValue(invocation, out Reference reference))
            {
                Reference = invocation;
                instance = reference.Object;
                referenced = true;
            }
            instance ??= ReturnOrAssign(invocation) ?? throw new ArgumentException($"Failed to load new instance from {invocation}, invocation returned null!");
            LoadInstance(instance);
            Reference = referenced ? invocation : null;
            return referenced ? reference : instance;
        }

        object? ReturnOrAssign(string invocation)
        {
            if (Assignment(invocation, out object? assigned))
                return assigned;
            invocation = invocation.Trim();
            if (References.TryGetValue(invocation, out Reference reference))
                return reference;
            if (invocation.EqualsCaseless("this"))
                return Object;
            string typeName = SimpleLex.ResolveMemberAccessOrLiteral(invocation, out string member, out bool field)?.Trim() ?? ImplicitRef?.StringID ?? throw new ArgumentException($"No type loaded to return fields from, or no type name given for isolated invocation.");
            if (!field)
            {
                TypeString declaringtype = TypeString.New(typeName);
                MethodString method = Lexer.MethodTemplate(member, declaringtype, ImplicitRef);
                return Invoker.InvokeMethod(method);
            }
            else
            {
                TypeString declaringtype = TypeString.New(typeName);
                FieldString fieldStr = new(member.Trim(), declaringtype);
                return Invoker.InvokeFieldOrProperty(fieldStr);
            }
        }

        Type Cast(string invocation)
        {
            if (Object == null)
                throw new InvalidOperationException("Cannot cast, instance is null.");
            TypeString tstring = TypeString.New(invocation.Trim());
            Type t = Reflector.FindType(tstring, true);
            if (!t.IsAssignableFrom(ObjectType))
                throw new InvalidCastException($"{ObjectType} cannot cast to {t}.");
            ImplicitRef = tstring;
            LoadTypeMembers(t);
            return t;
        }

        bool Assignment(string invocation, out object? assignedValue)
        {
            assignedValue = null;
            if (!SimpleLex.AssignmentSubstring(invocation, out string? left, out string? right))
                return false;
            string lefthand = left!;
            string righthand = right!;
            string? lefthandTypeName = SimpleLex.ResolveMemberAccessOrLiteral(lefthand, out lefthand, out bool lefthandfield);
            lefthand = lefthand.Trim();
            righthand = righthand.Trim();
            if (!lefthandfield)
                throw new ArgumentException($"Can only assign to fields or properties. Bad input: {lefthand}");
            Reflector.Assignment assignment = LefthandExpression(lefthand, lefthandTypeName, out Type lefthandtype, out object? lefthandInstance, invocation);
            assignedValue = RighthandExpression(righthand, assignment.ObjectType);
            assignment.SetValue(Invoker.TargetInstance(lefthandtype, lefthandInstance), assignedValue);
            return true;
        }

        Reflector.Assignment LefthandExpression(string lefthand, string? lefthandTypeName, out Type lefthandtype, out object? lefthandInstance, string invocation)
        {
            if (lefthandTypeName == null)
            {
                lefthandtype = StaticType ?? throw new ArgumentException($"There is no loaded type to assign fields to. Bad input: {invocation}");
                lefthandInstance = Object!;
            }
            else
            {
                TypeString typestring = TypeString.New(lefthandTypeName);
                FieldString fieldStr = new(lefthand, typestring);
                lefthandtype = Reflector.FindReference(fieldStr, out object? reference);
                lefthandInstance = reference;
            }
            if (lefthandtype != StaticType && (!lefthandtype?.IsClass ?? false))
                throw new NotSupportedException($"Assigning to the members of struct fields is currently unsupported due to constraints related to boxing.See \"Assignment\" in public API doc for more info.");
            FieldInfo? field = lefthandtype!.GetField(lefthand, Flag);
            if (field != null)
                return new Reflector.Assignment(field);
            else
            {
                PropertyInfo? prop = lefthandtype.GetProperty(lefthand, Flag) ?? throw new MissingMemberException($"No field or property found named {lefthand} in {lefthandtype}.");
                return new Reflector.Assignment(prop);
            }
        }

        object? RighthandExpression(string righthand, Type objecttype)
        {
            string? righthandTypeName = SimpleLex.ResolveMemberAccessOrLiteral(righthand, out righthand, out bool righthandfield);
            righthand = righthand.Trim();
            if (righthandTypeName != null)
            {
                righthandTypeName = righthandTypeName.Trim();
                TypeString typeStr = TypeString.New(righthandTypeName);
                if (righthandfield)
                {
                    FieldString fieldStr = new(righthand, typeStr);
                    return Invoker.InvokeFieldOrProperty(fieldStr);
                }
                else
                {
                    MethodString methodStr = Lexer.MethodTemplate(righthand, typeStr, ImplicitRef);
                    return Invoker.InvokeMethod(methodStr);
                }
            }
            else if (!righthandfield && SimpleLex.ImplicitThisMethodCall(righthand))
            {
                if (StaticType == null)
                    throw new ArgumentException("Cannot perform implicit this call, no type is loaded.");
                MethodString methodStr = Lexer.MethodTemplate(righthand, ImplicitRef!, ImplicitRef);
                return Invoker.InvokeMethod(methodStr);
            }
            else
            {
                ValueString valueStr = new(righthand);
                return Parser.ParseValue(valueStr, objecttype);
            }
        }

        string NewRef(string key)
        {
            if (SimpleLex.AssignmentSubstring(key, out string? left, out string? right))
            {
                left = left!.Trim();
                if (left.EqualsCaseless(Reference))
                    throw new NotSupportedException("Cannot reassign a reference if it is the currently loaded instance.");
                VerifyKey(left);
                object? value = RighthandExpression(right!, typeof(object));
                if (value == null)
                {
                    if (References.TryGetValue(left, out Reference reference1))
                    {
                        Reflector.NotNullable(reference1.Object.GetType(), value);
                        References.Remove(left);
                        return $"Removed reference {left}.";
                    }
                    return $"No reference named {left}.";
                }
                else
                {
                    Reference reference = new(value);
                    References[left] = reference;
                    return $"Assigned reference {left} :: {reference}.";
                }
            }
            else
            {
                if (Object == null)
                    throw new InvalidOperationException("No instance is loaded.");
                key = key.Trim();
                VerifyKey(key);
                // if (References.TryGetValue(key, out Reference existingref))
                // {
                //     if (!ReferenceEquals(Object, existingref.Object))
                //         throw new ArgumentException("Duplicate keyname detected.");
                //     return $"The current instance already is a reference named {key}.";
                // }
                References[key] = new(Object);
                Reference = key;
                return $"Added current instance as reference {key}.";
            }
        }
        string RemoveRef(string key)
        {
            key = key.Trim();
            if (key.EqualsCaseless(Reference))
                Reference = null;
            return References.Remove(key) ? $"{key} removed from references." : $"No reference named {key}.";
        }
        void LoadInstance(object instance)
        {
            Object = instance;
            LoadTypeMembers(ObjectType!);
            ImplicitRef = TypeString.ThisRef;
        }
        void LoadTypeMembers(Type type)
        {
            StaticType = type;
            TypeMap.MapType(Methods, Fields, type, Props);
        }

        public void Clear()
        {
            References.Clear();
            Methods.Clear();
            Fields.Clear();
            Props.Clear();
            LocalCache = null;
            Object = null;
            StaticType = null;
            ImplicitRef = null;
            Reference = null;
        }
        static void VerifyKey(string key)
        {
            TypeRegister.ThrowIfBadKey(key);
            if (TypeRegister.Contains(key))
                throw new ArgumentException($"Key {key} is taken by a cached type, and cannot be used as a name for a local reference. Names are not case sensitive.");
        }
        internal struct EmptyInstruction
        {


        }

    }
}
