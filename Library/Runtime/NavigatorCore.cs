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
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.Runtime
{


    internal sealed class NavigatorCore
    {
        ///.This is for the DynamicNavigator.
        internal TypeBook? LocalCache;
        internal readonly InvokeLexer _lexer = new();
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
       // public bool Caching = true;
        bool _chaining = false;
        internal const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public NavigatorCore()
        {
            _parser = new(this);
            _reflector = new(this);
            _invoker = new(this);
        }

        public object? Interface(string invocation, out bool chainexception) ///This is the primary and sole method for interfacing with the Navigator via strings.
        {

            chainexception = false;
            //  if (invocation.Length == 0 || string.IsNullOrWhiteSpace(invocation))
            //     return "No command detected.";
            char controller = default;
            int substring = -1;
            for (int i = 0; i < invocation.Length; i++) //skips leading whitespace
            {
                if (invocation[i] != ' ') //didnt feel like trimming here since we do a lot more trimming later on
                {
                    controller = invocation[i];
                    substring = i + 1;
                    break;
                }
            }
            if (substring == -1)
                return "No command detected.";
            return controller switch
            {
                '+' => AddViarable(invocation.Substring(substring)),
                '-' => RemoveVariable(invocation.Substring(substring)),
                '@' => LoadTypeStatic(invocation.Substring(substring)),
                '*' => LoadInstance(invocation.Substring(substring)),
                '^' => CastInstance(invocation.Substring(substring)),
                '~' => ChainInvoke(out chainexception, invocation.Substring(substring).Split(';')),
                '?' => Query(invocation.Substring(substring)),
                _ => InvokeOrAssign(invocation) //No controller
            };
        }


        IEnumerable<string> Query(string invocation)
        {
            if (invocation.EqualsCaseless("vars"))
                return _variables.Select(x => $"Key: {x.Key} :: {x.Value}");
            if (_loadedType == null)
                throw new InvalidOperationException("No loaded type to query. Use the static methods Fields/Methods/Props in class Types to query unloaded types.");
            if (MiniLexer.ImplicitThisMethodCall(invocation))
            {
                MethodString query = _lexer.MethodTemplate(invocation, _implicit_this!, _implicit_this);
                if (query.Name.EqualsCaseless("methods"))
                    return QueryParameters(_methods, query);
                if (query.Name.EqualsCaseless("fields"))
                    return QueryParameters(_fields, query);
                if (query.Name.EqualsCaseless("props"))
                {
                    if (query.Params.Count == 2)
                        throw new TargetParameterCountException("Local query for properties only supports max one paramter: a containing string.");
                    return QueryParameters(_props, query);
                }
            }
            else //parameterless default
            {
                if (invocation.EqualsCaseless("methods"))
                    return Types.ReadMembers(_methods, null, Flag);
                if (invocation.EqualsCaseless("fields"))
                    return Types.ReadMembers(_fields, null, Flag);
                if (invocation.Equals("props"))
                    return Types.ReadMembers(_props, null, Flag);
            }
            throw new ArgumentException("Invalid query");
        }

        IEnumerable<string> QueryParameters<K, V>(Dictionary<K, V> dic, MethodString query) where V : MemberInfo where K : notnull
        {
            if (query.Params.Count <= 2)
            {
                string? contains = null;
                if (query.Params.Count >= 1)
                    contains = (string?)_parser.ParameterToObject(query.Params[0], typeof(string));
                BindingFlags? flags = null;
                if (query.Params.Count == 2)
                    flags = (BindingFlags)_parser.ParameterToObject(query.Params[1], typeof(BindingFlags))!;
                flags ??= Flag;
                return Types.ReadMembers(dic, contains, flags.Value);
            }
            throw new TargetParameterCountException("Local query is max 2 params: a containig string and bindingflags search flags.");
        }

        List<string> ChainInvoke(out bool exception, params string[] commands)
        {
            exception = false;
            if (_chaining)
                throw new ArgumentException("Cannot invoke nested chains.");
            _chaining = true;
            List<string> invocations = new(commands.Length);
            for (int i = 0; i < commands.Length; i++)
            {
                string cmd = commands[i];
                try
                {
                    object? ret = Interface(cmd, out _);
                    if (ret is string s && s == "No command detected.")
                        continue;
                    string? retstring = ret is MemberInfo inf ? ReflectionPrinter.Print(inf, false) : ret?.ToString();
                    invocations.Add($"[Invocation: {cmd} :: Returned: {retstring ?? "null"}]");
                }
                catch (Exception ex)
                {
                    exception = true;
                    _chaining = false;
                    StringBuilder sb = new();
                    sb.CatchException(ex);
                    invocations.Insert(0, sb.ToString());
                    invocations.Add($"!!! EXCEPTION on [Invocation: {cmd}]");
                    return invocations;
                }
            }
            _chaining = false;
            return invocations;
        }



        public Type LoadTypeStatic(string typeName)
        {
            TypeString tstring = TypeString.New(typeName.Trim());
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
                FieldString fieldStr = new(member.Trim(), declaringtype);
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
            if (!MiniLexer.AssignmentSubstring(invocation, out string? left, out string? right))
                return false;
            string lefthand = left!.Trim();
            string righthand = right!;
            string? lefthandTypeName = MiniLexer.ResolveMemberAccess(lefthand, out lefthand, out bool lefthandfield);
            if (!lefthandfield)
                throw new ArgumentException($"Can only assign to fields, properties or variables. Bad input: {lefthand}");
            Type? lefthandtype;
            object? lefthandInstance;
            Reflector.Assignment assigningTo;
            // if (_variables.TryGetValue(lefthand, out VariableBinding? var))
            //     assigningTo = new(var);
            if (lefthandTypeName == null)
            {
                lefthandtype = _loadedType ?? throw new ArgumentException($"There is no loaded type to assign fields to. Bad input: {invocation}");
                lefthandInstance = _instance;
            }
            else
            {
                TypeString typestring = ThisOrNew(lefthandTypeName);
                FieldString fieldStr = new(lefthand, typestring);
                lefthandtype = _reflector.FindReference(fieldStr, out object? variable);
                lefthandInstance = variable;
            }
            if (lefthandtype != _loadedType && (!lefthandtype?.IsClass ?? false))
                throw new NotSupportedException($"Assigning to the members of struct fields is currently unsupported due to constraints related to boxing.See \"Assignment\" in public API doc for more info.");

            FieldInfo? field = lefthandtype!.GetField(lefthand, Flag);// ?? throw new MissingFieldException($"No field found in type {lefthandtype} named {lefthand}");
            if (field != null)
                assigningTo = new Reflector.Assignment(field);
            else
            {
                PropertyInfo? prop = lefthandtype.GetProperty(lefthand, Flag) ?? throw new MissingMemberException($"No field or property found named {lefthand} in {lefthandtype}.");
                assigningTo = new Reflector.Assignment(prop);
            }

            string? righthandTypeName = MiniLexer.ResolveMemberAccess(righthand, out righthand, out bool righthandfield);// ?? _key ?? throw new ArgumentException("No type loaded for implicit access on righthand side.");
            if (righthandTypeName != null)
            {
                TypeString typeStr = ThisOrNew(righthandTypeName.Trim());
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
            else if (!righthandfield && MiniLexer.ImplicitThisMethodCall(righthand))//this enables implicit this access, ie. field = method() for the lead method name
            {
                if (_loadedType == null)
                    throw new ArgumentException("Cannot perform implicit this call, no type is loaded.");
                MethodString methodStr = _lexer.MethodTemplate(righthand, _implicit_this!, _implicit_this);
                assignedValue = _invoker.InvokeMethod(methodStr);
            }
            else
            {
                ValueString valueStr = new(righthand.Trim());
                assignedValue = _parser.ParseValue(valueStr, assigningTo.ObjectType);
            }

            assigningTo.SetValue(lefthandInstance, assignedValue);
            // if (assignedValue != null && lefthandInstance != null && (lefthandInstance == _instance || var != null ))
            // {
            //     if (var != null)
            //     {
            //         LoadInstance(var.Object, var.ObjectType);
            //     }
            //     else
            //         LoadInstance(assignedValue, assignedValue.GetType());
            // }

            return true;
        }

        bool RemoveVariable(string key)
        {
            key = key.Trim();
            if (key.EqualsCaseless(_variable))
                _variable = null;
            return _variables.Remove(key);
        }

        bool AddViarable(string key)
        {
            if (_instance == null)
                throw new InvalidOperationException("No instance is loaded.");
            key = key.Trim();
            TypeCache.ThrowIfBadKey(key);
            if (TypeCache.s_registry.ContainsKey(new(key)))
                throw new ArgumentException($"Key {key} is already taken by a cached type, and cannot be used as a name for a local variable. Names are not case sensitive.");
            if (_variables.TryGetValue(key, out VariableBinding? variable))
            {
                if (!ReferenceEquals(_instance, variable.Object))
                    throw new ArgumentException("Duplicate keyname detected.");
                return false;
            }
            _variables[key] = new(_instance);
            _variable = key;
            return true;
        }



        public static void FlushStaticCache(bool ambiguousMatches = false, bool typeMembers = true, bool reifiedGenerics = true)
        {
            RuntimeCache.FlushStaticCache(ambiguousMatches, typeMembers, reifiedGenerics);
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
