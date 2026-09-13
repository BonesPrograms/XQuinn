using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Reflection;
using XQuinn.CodeAnalysis.AST;
using System.Linq;
using System;
using System.Collections.Generic;
using XQuinn.CodeAnalysis;
using System.Text;
using System.Collections;
using System.Runtime.ExceptionServices;
using XQuinn.Private.NavigatorEngine;

namespace XQuinn.Runtime
{


    internal sealed class Navigator
    {
        ///.This is for the DynamicNavigator.
        internal TypeBook? LocalCache;
        readonly InvokeLexer _lexer = new();
        internal readonly Parser _parser;
        internal readonly Reflector _reflector;
        internal readonly Invoker _invoker;
        internal object? _instance;
        internal string? _variable;
        internal Type? _instanceType; ///Raw type of the instance
        internal Type? _loadedType; ///Loaded type; could vary from instance type via cast.
        internal TypeString? _implicit_this; ///Data referring back to the loaded type that works within the MethodLexer
        internal readonly Dictionary<MethodKey, MethodBase> _methods = new();
        internal readonly Dictionary<string, FieldInfo> _fields = new(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, PropertyInfo> _props = new(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, VariableBinding> _variables = new(StringComparer.OrdinalIgnoreCase);
        public bool Caching = true;
        internal const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public Navigator()
        {
            _parser = new(this);
            _reflector = new(this);
            _invoker = new(this);
        }

        public object? Interface(string invocation) ///This is the primary and sole method for interfacing with the Navigator via strings.
        {
            if (invocation.Length == 0 || string.IsNullOrWhiteSpace(invocation))
                return "No command detected.";
            return invocation[0] switch
            {
                '+' => AddViarable(invocation.Substring(1)),
                '-' => RemoveVariable(invocation.Substring(1)),
                '@' => LoadTypeStatic(invocation.Substring(1)),
                '*' => LoadInstance(invocation.Substring(1)),
                '^' => CastInstance(invocation.Substring(1)),
                '~' => ChainInvoke(invocation.Substring(1).Split(';'
#if NET6_0_OR_GREATER
                , StringSplitOptions.TrimEntries
#endif
                )),
                _ => InvokeOrAssign(invocation)
            };
        }
        List<string> ChainInvoke(params string[] commands)
        {
            List<string> invocations = new(commands.Length);
            for (int i = 0; i < commands.Length; i++)
            {
                string cmd = commands[i].Trim();
                try
                {
                    object? ret = Interface(cmd);
                    if (ret is string s && s == "No command detected.")
                        continue;
                    invocations.Add($"[Invocation: {cmd} :: Returned: {ret ?? "null"}]");
                }
                catch (Exception ex)
                {
                    StringBuilder sb = new();
                    sb.CatchException(ex);
                    invocations.Insert(0, sb.ToString());
                    invocations.Add($"!!! EXCEPTION on [Invocation: {cmd}]");
                    return invocations;
                }
            }
            return invocations;
        }



        public Type LoadTypeStatic(string typeName)
        {
            TypeString tstring = TypeString.New(typeName);
            Type t = _reflector.FindType(tstring, true);
            _implicit_this = tstring;
            LoadTypeMembers(t);
            _instance = null;
            _instanceType = null;
            _variable = null;
            return t;
        }

        void LoadInstance(object instance, Type instanceType)
        {
            _instance = instance;
            LoadTypeMembers(instanceType);
            _instanceType = instanceType;
            _implicit_this = TypeString.s_this;
        }
        void LoadTypeMembers(Type type)
        {
            _loadedType = type;
            TypeMap.MapType(_methods, _fields, type, _props);
        }
        //Checks for method or field syntax. If it detects a method, it diverts to an isolated type load and method invocation.
        object LoadInstance(string invocation)
        {
            object? instance = null;
            Type? objectType = null;
            _variable = null;
            if (_variables.TryGetValue(invocation, out VariableBinding? variable))
            {
                _variable = invocation;
                instance = variable.Object;
                objectType = variable.ObjectType;
            }
            instance ??= InvokeOrAssign(invocation) ?? throw new ArgumentException($"Failed to load new instance from {invocation}, invocation returned null!");
            objectType ??= instance.GetType();
            LoadInstance(instance, objectType);
            return variable ?? instance;
        }
        //Isolated lexing, loading and invocation for "quick invocation" without resetting loaded instance, method or type.
        object? InvokeOrAssign(string invocation)
        {
            if (Assignment(invocation, out object? assigned))
                return assigned;
            string typeName = MiniLexer.ResolveMemberAccess(invocation, out string member, out bool field) ?? _implicit_this?.StringID ?? throw new ArgumentException($"No type loaded to return fields from, or no type name given for isolated invocation.");
            if (!field)
            {
                TypeString declaringtype = ThisOrNew(typeName);
                MethodString method = _lexer.MethodTemplate(member, declaringtype, _implicit_this);
                return _invoker.InvokeMethod(method);
            }
            else
            {
                if (member.EqualsCaseless("this"))
                    return _instance ?? "null";
                if (_variables.TryGetValue(member, out VariableBinding? variable))
                    return variable;
                TypeString declaringtype = ThisOrNew(typeName);
                FieldString fieldStr = new(member, declaringtype);
                return _invoker.InvokeFieldOrProperty(fieldStr);
            }

        }

        Type CastInstance(string invocation)
        {
            if (_instance == null)
                throw new InvalidOperationException("Cannot cast, instance is null.");
            TypeString tstring = TypeString.New(invocation);
            Type t = _reflector.FindType(tstring, true);
            _implicit_this = tstring;
            if (!t.IsAssignableFrom(_instanceType))
                throw new InvalidCastException($"{_instanceType} cannot cast to {t}.");
            LoadTypeMembers(t);
            return t;
        }

        bool Assignment(string invocation, out object? assignedValue)
        {
            assignedValue = null;
            string[] assignment = invocation.Split('=');
            if (assignment.Length == 1)
                return false;
            if (assignment.Length != 2)
                throw new ArgumentException($"Invalid assignment, can only contain left hand and right hand. Bad assignment: {invocation}");

            string lefthand = assignment[0];
            string righthand = assignment[1];
            string? lefthandTypeName = MiniLexer.ResolveMemberAccess(lefthand, out lefthand, out bool lefthandfield);
            if (!lefthandfield)
                throw new ArgumentException($"Can only assign to fields. Bad input: {lefthand}");

            Type lefthandtype;
            object? lefthandInstance;
            lefthand = lefthand.Trim();
            if (lefthandTypeName == null)
            {
                lefthandtype = _loadedType ?? throw new ArgumentException($"There is no loaded type to assign fields to. Bad input: {invocation}");
                lefthandInstance = _instance;
            }
            else
            {
                TypeString typestring = ThisOrNew(lefthandTypeName);
                FieldString fieldStr = new(lefthand, typestring);
                lefthandtype = _reflector.FindObject(fieldStr, out object? variable);
                lefthandInstance = variable;
            }
            if (!lefthandtype.IsClass)
                throw new NotSupportedException($"Assigning to the members of struct fields is currently unsupported due to constraints related to boxing. Loading the struct field as the Navigator's instance also will not work for assignment; changes will not be reflected in the target field. You must recreate the struct entirely with your modified values and assign it to the target field.");
            Reflector.AssignableMember assigningTo;
            FieldInfo? field = lefthandtype.GetField(lefthand, Flag);// ?? throw new MissingFieldException($"No field found in type {lefthandtype} named {lefthand}");
            if (field != null)
                assigningTo = Reflector.AssignableMember.New(field);
            else
            {
                PropertyInfo? prop = lefthandtype.GetProperty(lefthand, Flag) ?? throw new MissingMemberException($"No field or property found named {lefthand} in {lefthandtype}.");
                assigningTo = Reflector.AssignableMember.New(prop);
            }
            string? righthandTypeName = MiniLexer.ResolveMemberAccess(righthand, out righthand, out bool righthandfield);// ?? _key ?? throw new ArgumentException("No type loaded for implicit access on righthand side.");
            if (righthandTypeName != null)
            {
                TypeString typeStr = ThisOrNew(righthandTypeName);
                if (righthandfield)
                {
                    FieldString fieldStr = new(righthand, typeStr);
                    assignedValue = _invoker.InvokeFieldOrProperty(fieldStr);
                }
                else
                {
                    MethodString methodStr = _lexer.MethodTemplate(righthand, typeStr, _implicit_this); //implicit this for subparameters only
                    assignedValue = _invoker.InvokeMethod(methodStr);
                }
            }
            else if (MiniLexer.ImplicitThisMethodCall(righthand))//this enables implicit this access, ie. field = method() for the lead method name
            {
                if (_implicit_this == null)
                    throw new ArgumentException("Cannot perform implicit this call, no type is loaded.");
                MethodString methodStr = _lexer.MethodTemplate(righthand, _implicit_this, _implicit_this);
                assignedValue = _invoker.InvokeMethod(methodStr);
            }
            else
            {
                ValueString valueStr = new(righthand.Trim());
                assignedValue = _parser.ParseValue(valueStr, assigningTo.MemberType);
            }

            assigningTo.SetValue(lefthandInstance, assignedValue);
            return true;
        }

        bool RemoveVariable(string key)
        {
            if (key.EqualsCaseless(_variable))
                _variable = null;
            return _variables.Remove(key);
        }

        bool AddViarable(string key)
        {
            if (_instance == null)
                throw new InvalidOperationException("No instance is loaded.");
            TypeCache.ThrowIfBadKey(key);
            if (TypeCache.s_registry.ContainsKey(new(key)))
                throw new ArgumentException($"Key {key} is already taken by a cached type, and cannot be used as a name for a local variable. Names are not case sensitive.");
            if (_variables.TryGetValue(key, out VariableBinding? variable))
            {
                if (!ReferenceEquals(_instance, variable.Object))
                    throw new ArgumentException("Duplicate keyname detected.");
                return false;
            }
            _variables[key] = new(_instance, _instanceType!);
            _variable = key;
            return true;
        }



        public static void FlushStaticCache(bool ambiguousMatches = false, bool typeMembers = true, bool reifiedGenerics = true)
        {
            InternalCache.FlushStaticCache(ambiguousMatches, typeMembers, reifiedGenerics);
        }


        public void Clear() ///This is for DynamicNavigator
        {

            _variables.Clear();
            _methods.Clear();
            _fields.Clear();
            LocalCache = null;
            _instance = null;
            _instanceType = null;
            _loadedType = null;
        }
        // }


        TypeString ThisOrNew(string typename)
        {
            // return _implicit_this?.NameWithGenerics.EqualsCaseless(typename) ?? false ? _implicit_this : TypeString.New(typename);
            return typename.EqualsCaseless(_implicit_this?.StringID) ? _implicit_this! : TypeString.New(typename);
        }


    }

}
