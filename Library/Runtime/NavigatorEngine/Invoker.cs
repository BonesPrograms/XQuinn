using System.Reflection;
using XQuinn.Extensions;

using XQuinn.LangInterp.SyntaxTree;

using System;
using System.Collections.Generic;

using System.Runtime.ExceptionServices;

namespace XQuinn.Runtime.NavigatorEngine
{
    sealed class Invoker : CoreObject
    {
        public Invoker(NavigatorCore navig) : base(navig)
        {
        }

        internal object? InvokeFieldOrProperty(FieldString fieldstring)
        {
            Type fromType = _core._reflector.FindReference(fieldstring, out object? variable);
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
            if (fieldOrProp == null && fromType == _core._static_type)
            {
                if (_core._props.TryGetValue(fname, out PropertyInfo? propMember))
                    fieldOrProp = propMember;
            }
            else if (fieldOrProp == null && fromType != _core._static_type)
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
            if (fieldOrProp == null && fromType == _core._static_type)
            {
                if (_core._fields.TryGetValue(fname, out FieldInfo? fieldMember))
                    fieldOrProp = fieldMember;
            }
            else if (fieldOrProp == null && fromType != _core._static_type)
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
            Type fromType = _core._reflector.FindReference(mthdString, out object? variable);
            MethodBase? call = RuntimeCache.FromCache<MethodBase>(mthdString.StringID, fromType, out bool typeCached, out bool methodCached);
            ParameterInfo[]? args = null;
            ExternalMethod(ref call, ref args, fromType, mthdString);
            call ??= fromType == _core._static_type ? _core._reflector.FindMethod(mthdString) : throw new MissingMethodException($"No method named {mthdString.Name} with generic arg count {mthdString.Generics.Count} found in {fromType}'s methods or overload resolutions.");
            ConvertGeneric(ref call, ref args, mthdString);
            args ??= call.GetParameters();
            RuntimeCache.CacheMember(typeCached, methodCached, fromType, call, mthdString.StringID);
            return FinalizeInvoke(call, args, mthdString, fromType, variable);
        }
        void ExternalMethod(ref MethodBase? call, ref ParameterInfo[]? args, Type fromType, MethodString mthdString)
        {
            if (call == null && fromType != _core._static_type)
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
            return variable ?? (fromType.IsAssignableFrom(_core._object_type) ? _core._object : null);
        }

        object? FinalizeInvoke(MethodBase call, ParameterInfo[] parameters, MethodString mthdString, Type fromType, object? variable)
        {
            object? obj = null;
            object?[] args = _core._parser.ParseParameters(parameters, mthdString);
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
                    call = mthdString.ConvertToGeneric(mthd, _core.LocalCache);
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
            MethodKey mkey = MethodKey.MethodQuery(mthdString);
            IEnumerable<MethodBase> methodbases = TypeMap.GetUnfilteredMethods(fromType, mkey.GKey.Name.EqualsCaseless("new"));
            int matches = 0;
            foreach (MethodBase method in methodbases)
            {
                GenericKey gkey = new(method);
                if (gkey == mkey.GKey)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (Reflector.SupportedMember(method, parameters))
                    {
                        if (matches == mkey.MatchIndex)
                        {
                            args = parameters;
                            call = method;
                            break;
                        }
                        matches++;
                    }
                }
            }
        }
    }
}
