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

namespace XQuinn.Runtime.NavigatorEngine
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
            string fname = fieldstring.Name;
            MemberInfo? fieldOrProp = RuntimeCache.FromCache<MemberInfo>(fieldstring.Name, fromType, out bool typeCached, out bool memberCached);
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
               RuntimeCache.CacheMember(typeCached, memberCached, fromType, fieldOrProp, fieldstring.Name);
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
                 RuntimeCache.CacheMember(typeCached, memberCached, fromType, fieldOrProp, fieldstring.Name);
                ret = field.GetValue(TargetInstance(fromType, variable));
                return true;
            }
            return false;

        }
        internal object? InvokeMethod(MethodString mthdString)
        {
            Type fromType = _reflector.FindReference(mthdString, out object? variable);
            MethodBase? call = RuntimeCache.FromCache<MethodBase>(mthdString.StringID, fromType, out bool typeCached, out bool methodCached);
            ParameterInfo[]? args = null;
            ExternalMethod(ref call, ref args, fromType, mthdString);
            call ??= fromType == _loadedType ? _reflector.FindMethod(mthdString) : throw new MissingMethodException($"No method named {mthdString.Name} with generic arg count {mthdString.Generics.Count} found in {fromType}'s methods or overload resolutions.");
            ConvertGeneric(ref call, ref args, mthdString);
            args ??= call.GetParameters(); //generics get params first, nongenerics get after
             RuntimeCache.CacheMember(typeCached, methodCached, fromType, call, mthdString.StringID);
            return FinalizeInvoke(call, args, mthdString, fromType, variable);
        }
        void ExternalMethod(ref MethodBase? call, ref ParameterInfo[]? args, Type fromType, MethodString mthdString)
        {
            if (call == null && fromType != _loadedType)
            {
                bool ambiguousMatch = RuntimeCache.CheckAmbiguousMatch(fromType, mthdString, out Type cachedType, out HashSet<string>? cachedMatches);
                if (!ambiguousMatch)
                {
                    try
                    {
                        call = fromType.GetMethod(mthdString.Name, NavigatorCore.Flag);
                    }
                    catch (AmbiguousMatchException)
                    {
                        RuntimeCache.CacheAmbiguousMatch(cachedMatches, cachedType, mthdString);
                        // throw new AmbiguousMatchException($"Method named {mthdString.String} in type {fromType} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
                    }
                    if (call != null)
                    {
                        args = call.GetParameters();
                        if (!Reflector.SupportedMember(call, args))
                            throw new ArgumentException($"Method {call} in type {call.DeclaringType} has unsupported in out or ref params or ref returntype, or is a static constructor.");
                    }
                }
                if (call == null || ambiguousMatch)
                {
                    SortAmbiguousMatch(mthdString, fromType, out args, out call);
                }
            }

        }


        internal object? TargetInstance(Type fromType, object? variable)
        {
            return variable ?? (fromType.IsAssignableFrom(_instanceType) ? _instance : null);
        }

        object? FinalizeInvoke(MethodBase call, ParameterInfo[] parameters, MethodString mthdString, Type fromType, object? variable)
        {
            object? obj = null;
            object?[] args = _parser.ParseParameters(parameters, mthdString);
            try
            {
                obj = call is ConstructorInfo ctor ? ctor.Invoke(args) : call.Invoke(TargetInstance(fromType, variable), args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            return obj;
        }

        void ConvertGeneric(ref MethodBase call, ref ParameterInfo[]? args, MethodString mthdString)
        {
            if (call is MethodInfo mthd)
            {
                if (mthd.IsGenericMethodDefinition)
                {
                    call = mthdString.ConvertToGeneric(mthd, LocalCache);
                    args = call.GetParameters();
                }
                else if (!mthd.IsGenericMethod && mthdString.Generics.Count > 0)
                    throw new ArgumentException($"method {call} cannot accept type arguments.");
            }
        }

        static void SortAmbiguousMatch(MethodString mthdString, Type fromType, out ParameterInfo[]? args, out MethodBase? call)
        {
            args = null;
            call = null;
            MethodKey query = MethodKey.MethodQuery(mthdString);
            IEnumerable<MethodBase> methodbases = TypeMap.GetUnfilteredMethods(fromType, query.GenericKey.Key.EqualsCaseless("new"));
            int i = 0;
            foreach (MethodBase method in methodbases)
            {
                GenericKey key = TypeMap.MethodGenericKey(method);
                if (key == query.GenericKey) //method.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (Reflector.SupportedMember(method, parameters))
                    {
                        if (i == query.OverloadIndex)
                        {
                            args = parameters;
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

