using System.Collections;
using XQuinn.Runtime;
using System;
using XQuinn.CodeAnalysis.AST;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis;
using System.Reflection;
using System.Collections.Generic;
using System.Net;

namespace XQuinn.Private.NavigatorEngine
{
    sealed class LocalQuery : ExpandedCoreObject
    {
        public LocalQuery(NavigatorCore core) : base(core)
        {

        }

        TypeString? _implicit_this => _core._implicit_this;
        InvokeLexer _lexer => _core._lexer;
        Invoker _invoker => _core._invoker;
        Parser _parser => _core._parser;
        static readonly ParameterInfo[] _field_or_method_params =
        typeof(Types)
        .GetMethod(nameof(Types.Fields), new Type[] { typeof(Type), typeof(string), typeof(BindingFlags) })!
        .GetParameters();

        static readonly ParameterInfo[] _prop_params = GetPropParams();
        static ParameterInfo[] GetPropParams()
        {
            ParameterInfo[] parameters = new ParameterInfo[_field_or_method_params.Length - 1];
            for (int i = 0; i < parameters.Length; i++)
                parameters[i] = _field_or_method_params[i];
            return parameters;
        }
        IEnumerable<string> Query(string invocation)
        {
            if (_loadedType == null)
                throw new InvalidOperationException("No loaded type to query.");
            MethodString query = _lexer.MethodTemplate(invocation, _implicit_this!, _implicit_this);
            if (query.NameOrValue.EqualsCaseless("methods"))
            {
                return Query(_core._methods, query);
            }
            if(query.NameOrValue.EqualsCaseless("fields"))
            return Query(_core._fields, query);
            if(query.NameOrValue.EqualsCaseless("props"))
            {
                if(query.Params.Count==2)
                throw new TargetParameterCountException("Local query for props only supports one paramter: a containing string.");
                return Query(_core._props, query);
            }
            if(query.NameOrValue.EqualsCaseless("vars"))
            {
                
            }

        }

         IEnumerable<string> Query<K, V>(Dictionary<K, V> dic, MethodString query) where V : MemberInfo
        {
            if (query.Params.Count == 0)
                return Query(dic, null, null);
            if (query.Params.Count == 1)
            {
                string? value = (string?)_parser.ParameterToObject(query.Params[0], typeof(string));
                return Query(dic, value, null);
            }
            if(query.Params.Count == 2)
            {
                string? value = (string?)_parser.ParameterToObject(query.Params[0], typeof(string));
                BindingFlags flags = (BindingFlags)_parser.ParameterToObject(query.Params[1], typeof(BindingFlags))!
                return Query(dic, value, flags);
            }
            throw new TargetParameterCountException("Local query is max 2 params: a containg string and bindingflags search flags.");
        }

        static IEnumerable<string> Query<K, V>(Dictionary<K, V> dic, string? key, BindingFlags? flags) where V : MemberInfo
        {
            flags ??= NavigatorCore.Flag;
            return Types.ReadMembers(dic, key, flags.Value);
        }
    }
}