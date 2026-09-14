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
using XQuinn.Runtime;

namespace XQuinn.Private.NavigatorEngine
{


    sealed class Invoker : ExpandedCoreObject
    {

        Reflector _reflector => _core._reflector;

        Parser _parser => _core._parser;

        public Invoker(NavigatorCore navig) : base(navig)
        {

        }

        internal object? InvokeFieldOrProperty(FieldString fieldstring)
        {
            Type fromType = _reflector.FindReference(fieldstring, out object? variable);
            string fname = fieldstring.NameOrValue;
            MemberInfo? fieldOrProp = InternalCache.FromCache<MemberInfo>(fieldstring.NameOrValue, fromType, out bool typeCached, out bool memberCached);
            if (ReturnField(fieldOrProp, fromType, fname, fieldstring, typeCached, memberCached, variable, out object? ret))
                return ret;
            if (ReturnProperty(fieldOrProp, fromType, fname, fieldstring, typeCached, memberCached, variable, out ret))
                return ret;
            throw new MissingMemberException($"No field or property found named {fname} in {fromType}.");
        }

        bool ReturnProperty(MemberInfo? fieldOrProp, Type fromType, string fname, FieldString fieldstring, bool typeCached, bool memberCached, object? variable, out object? ret)
        {
            ret = null;
            if (fieldOrProp == null && fromType == _loadedType)
            {
                if (_props.TryGetValue(fname, out PropertyInfo? propMember))
                    fieldOrProp = propMember;
            }
            else if (fieldOrProp == null)
            {
                PropertyInfo? getprop = fromType.GetProperty(fname, NavigatorCore.Flag);
                if (getprop?.GetIndexParameters().Length > 0)
                    throw new NotSupportedException("Indexers must be invoked using their backing method.");
                fieldOrProp = getprop;
            }
            if (fieldOrProp is PropertyInfo prop)
            {
                CacheMember(typeCached, memberCached, fromType, fieldOrProp, fieldstring.NameOrValue);
                ret = prop.GetValue(TargetInstance(fromType, variable), NavigatorCore.Flag, null, null, null);
                return true;
            }
            return false;
        }

        bool ReturnField(MemberInfo? fieldOrProp, Type fromType, string fname, FieldString fieldstring, bool typeCached, bool memberCached, object? variable, out object? ret)
        {
            ret = null;
            if (fieldOrProp == null && fromType == _loadedType)
            {
                if (_fields.TryGetValue(fname, out FieldInfo? fieldMember))
                    fieldOrProp = fieldMember;
            }
            else if (fieldOrProp == null)
                fieldOrProp = fromType.GetField(fname, NavigatorCore.Flag);
            if (fieldOrProp is FieldInfo field)
            {
                CacheMember(typeCached, memberCached, fromType, fieldOrProp, fieldstring.NameOrValue);
                ret = field.GetValue(TargetInstance(fromType, variable));
                return true;
            }
            return false;

        }
        internal object? InvokeMethod(MethodString mthdString)
        {
            Type fromType = _reflector.FindReference(mthdString, out object? variable);
            MethodBase? call = InternalCache.FromCache<MethodBase>(mthdString.StringID, fromType, out bool typeCached, out bool methodCached);
            ParameterInfo[]? parameters = null;
            ExternalMethod(ref call, ref parameters, fromType, mthdString);
            call ??= fromType == _loadedType ? _reflector.FindMethod(mthdString) : throw new MissingMethodException($"No method named {mthdString.NameOrValue} with generic arg count {mthdString.Generics.Count} found in {fromType}'s methods or overload resolutions.");
            ConvertGeneric(ref call, ref parameters, mthdString);
            parameters ??= call.GetParameters(); //generics get params first, nongenerics get after
            CacheMember(typeCached, methodCached, fromType, call, mthdString.StringID);
            return FinalizeInvoke(call, parameters, mthdString, fromType, variable);
        }
        void ExternalMethod(ref MethodBase? call, ref ParameterInfo[]? parameters, Type fromType, MethodString mthdString)
        {
            if (call == null && fromType != _loadedType)
            {
                bool ambiguousMatch = InternalCache.CheckAmbiguousMatch(fromType, mthdString, out Type cachedType, out HashSet<string>? cachedMatches);
                if (!ambiguousMatch)
                {
                    try
                    {
                        call = fromType.GetMethod(mthdString.NameOrValue, NavigatorCore.Flag);
                    }
                    catch (AmbiguousMatchException)
                    {
                        InternalCache.CacheAmbiguousMatch(cachedMatches, cachedType, mthdString);
                        // throw new AmbiguousMatchException($"Method named {mthdString.String} in type {fromType} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
                    }
                    if (call != null)
                    {
                        parameters = call.GetParameters();
                        if (!Reflector.SupportedMember(call, parameters))
                            throw new ArgumentException($"Method {call} in type {call.DeclaringType} has unsupported in out or ref params or ref returntype");
                    }
                }
                if (call == null || ambiguousMatch)
                {
                    SortAmbiguousMatch(mthdString, fromType, out parameters, out call);
                }
            }

        }
        void CacheMember(bool typeCached, bool memberCached, Type fromType, MemberInfo member, string key)
        {
            if (Caching)
            {
                InternalCache.CacheMember(typeCached, memberCached, fromType, member, key);
            }
        }

        object? TargetInstance(Type paramType, object? variable)
        {
            return variable ?? (paramType.IsAssignableFrom(_instanceType) ? _instance : null);
        }

        object? FinalizeInvoke(MethodBase call, ParameterInfo[] parameters, MethodString mthdString, Type fromType, object? variable)
        {
            object? obj = null;
            object?[] parsedparams = _parser.ParseParameters(parameters, mthdString);
            try
            {
                obj = call is ConstructorInfo ctor ? ctor.Invoke(parsedparams) : call.Invoke(TargetInstance(fromType, variable), parsedparams);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            return obj;
        }

        void ConvertGeneric(ref MethodBase call, ref ParameterInfo[]? parameters, MethodString mthdString)
        {
            if (call is MethodInfo mthd)
            {
                if (mthd.IsGenericMethodDefinition)
                {
                    call = mthdString.ConvertToGeneric(mthd, LocalCache);
                    parameters = call.GetParameters();
                }
                else if (!mthd.IsGenericMethod && mthdString.Generics.Count > 0)
                    throw new ArgumentException($"method {call} cannot accept type arguments.");
            }
        }

        static void SortAmbiguousMatch(MethodString mthdString, Type fromType, out ParameterInfo[]? parameters, out MethodBase? call)
        {
            parameters = null;
            call = null;
            MethodKey query = MethodKey.MethodQuery(mthdString);
            IEnumerable<MethodBase> methodbases = TypeMap.GetUnfilteredMethods(fromType, query.GenericKey.Key.EqualsCaseless("new"));
            int i = 0;
            foreach (MethodBase method in methodbases)
            {
                GenericKey key = TypeMap.MethodGenericKey(method);
                if (key == query.GenericKey) //method.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                {
                    ParameterInfo[] methodparams = method.GetParameters();
                    if (Reflector.SupportedMember(method, methodparams))
                    {
                        if (i == query.OverloadIndex)
                        {
                            parameters = methodparams;
                            call = method;
                            break;
                        }
                        i++;
                    }
                }
            }
        }

    }

}

