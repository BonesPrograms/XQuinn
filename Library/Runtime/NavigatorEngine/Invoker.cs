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
            Type fromType = Core.Reflector.FindReference(fieldstring, out object? reference);
            string fname = fieldstring.Name;
            MemberInfo? fieldOrProp = RuntimeCache.FromCache<MemberInfo>(fieldstring.Name, fromType, out bool typeCached, out bool memberCached);
            if (ReturnField(fieldOrProp, fromType, fname, fieldstring, typeCached, memberCached, reference, out object? ret))
                return ret;
            if (ReturnProperty(fieldOrProp, fromType, fname, fieldstring, typeCached, memberCached, reference, out ret))
                return ret;
            throw new MissingMemberException($"No field or property found named {fname} in {fromType}.");
        }

        bool ReturnProperty(MemberInfo? fieldOrProp, Type fromType, string fname, FieldString fieldstring, bool typeCached, bool memberCached, object? reference, out object? ret)
        {
            ret = null;
            if (fieldOrProp == null)
            {
                if (fromType == Core.StaticType)
                {
                    if (Core.Props.TryGetValue(fname, out PropertyInfo? propMember))
                        fieldOrProp = propMember;
                }
                else
                {
                    PropertyInfo? getprop = fromType.GetProperty(fname, NavigatorCore.Flag);
                    if (getprop?.GetIndexParameters().Length > 0)
                        throw new NotSupportedException("Indexers must be invoked using their backing method.");
                    fieldOrProp = getprop;
                    if (fieldOrProp != null && TypeMap.HiddenFromNavigator(fieldOrProp))
                        fieldOrProp = null;
                }
            }
            if (fieldOrProp is PropertyInfo prop)
            {
                RuntimeCache.CacheMember(typeCached, memberCached, fromType, fieldOrProp, fieldstring.Name);
                ret = prop.GetValue(TargetInstance(fromType, reference), NavigatorCore.Flag, null, null, null);
                return true;
            }
            return false;
        }

        bool ReturnField(MemberInfo? fieldOrProp, Type fromType, string fname, FieldString fieldstring, bool typeCached, bool memberCached, object? reference, out object? ret)
        {
            ret = null;
            if (fieldOrProp == null)
            {
                if (fromType == Core.StaticType)
                {
                    if (Core.Fields.TryGetValue(fname, out FieldInfo? fieldMember))
                        fieldOrProp = fieldMember;
                }
                else
                {
                    fieldOrProp = fromType.GetField(fname, NavigatorCore.Flag);
                    if (fieldOrProp != null && TypeMap.HiddenFromNavigator(fieldOrProp))
                        fieldOrProp = null;
                }
            }
            if (fieldOrProp is FieldInfo field)
            {
                RuntimeCache.CacheMember(typeCached, memberCached, fromType, fieldOrProp, fieldstring.Name);
                ret = field.GetValue(TargetInstance(fromType, reference));
                return true;
            }
            return false;
        }
        internal object? InvokeMethod(MethodString mthdString)
        {
            Type fromType = Core.Reflector.FindReference(mthdString, out object? reference);
            MethodBase? call = RuntimeCache.FromCache<MethodBase>(mthdString.StringID, fromType, out bool typeCached, out bool methodCached);
            ParameterInfo[]? args = null;
            ExternalMethod(ref call, ref args, fromType, mthdString);
            call ??= fromType == Core.StaticType ? Core.Reflector.FindMethod(mthdString) : throw new MissingMethodException($"No method named {mthdString.Name} with generic arg count {mthdString.Generics.Count} found in {fromType}'s methods or overload resolutions.");
            ConvertGeneric(ref call, ref args, mthdString);
            args ??= call.GetParameters();
            RuntimeCache.CacheMember(typeCached, methodCached, fromType, call, mthdString.StringID);
            return FinalizeInvoke(call, args, mthdString, fromType, reference);
        }
        void ExternalMethod(ref MethodBase? call, ref ParameterInfo[]? args, Type fromType, MethodString mthdString)
        {
            if (call == null && fromType != Core.StaticType)
            {

                bool ambiguousMatch = RuntimeCache.CheckAmbiguousMatch(fromType, mthdString, out Type cachedType, out HashSet<string>? cachedMatches);
                if (!mthdString.Name.EqualsCaseless("new") || !mthdString.Name.Contains(':') || !ambiguousMatch)
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
                        if (TypeMap.HiddenFromNavigator(call))
                            call = null;
                        else
                        {
                            args = call.GetParameters();
                            if (!Reflector.SupportedMember(call, args))
                                throw new ArgumentException($"Method {call} in type {call.DeclaringType} has unsupported in out or ref params or ref returntype, or is a static constructor.");
                        }
                    }
                }
                if (call == null || ambiguousMatch)
                {
                    SortAmbiguousMatch(mthdString, fromType, out args, out call);
                }
            }
        }

        internal object? TargetInstance(Type fromType, object? reference)
        {
            return reference ?? (fromType.IsAssignableFrom(Core.ObjectType) ? Core.Object : null);
        }

        object? FinalizeInvoke(MethodBase call, ParameterInfo[] parameters, MethodString mthdString, Type fromType, object? reference)
        {
            object? obj = null;
            object?[] args = Core.Parser.ParseParameters(parameters, mthdString);
            try
            {
                obj = call is ConstructorInfo ctor ? ctor.Invoke(args) : call.Invoke(TargetInstance(fromType, reference), args);
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
                    call = mthdString.ConvertToGeneric(mthd, Core.LocalCache);
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
                if (!TypeMap.HiddenFromNavigator(method))
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
}
