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

        //TargetInstance is an old method back from before the navigator was able to treat fields, variables and properties as if they were types that can be accessed
        //and their members invoked. Typically, when you use a varaible, field or property as a "type", the system returns the actual object instance alongside the type, and it does so
        //early - the moment you search for the type member, which happens before we actually search for members inside the member's type itself.

        //TargetInstance works the opposite. If youre invoking something that requires the currently loaded instance, the system does not retrieve the instance early
        //Instead, it retrieves the type, be it the instance type, or a base type, depending on whether or not youre casting, and then later on,
        //it passes that type to TargetInstance. TargetInstance quickly checks if the current instancetype polymorphs into the accessed type,
        //and if it does, it returns the currently loaded instance. This was again designed back when there was no member access, so it made sense to wrap things up
        //and get the target instance right before invocation. Back then, there was no "FindReference", only "FindType", which could get the loaded type, instance type,
        // or a static type, without returning the current instance. I havent yet found it necessary to migrate the code relating to getting the instance type up to "FindReference". 
        // FindReference does not check for if youre looking for the currently loaded type or instance, it only checks for members or variables. So for now this will be "backwards".

        //As you can see, we had to modify the system a bit once we introduced using members and variables as "types". Because we retrieve those early, if TargetInstance's
        //variable value is not null, it means, "hey, we already found the reference this method is from, dont worry about it ,just return the object we passed to you"
        //To prevent issues where if a member or variable is the same type as the loaded type and the system returns the loaded instance instead of the accessed member or variable.

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
            IEnumerable<MethodBase> methodbases = TypeMap.GetUnfilteredMethods(fromType, query.GenericID.Name.EqualsCaseless("new"));
            int matches = 0;
            foreach (MethodBase method in methodbases)
            {
                GenericKey key = TypeMap.MethodGenericKey(method);
                if (key == query.GenericID) //method.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (Reflector.SupportedMember(method, parameters))
                    {
                        if (matches == query.MatchIndex)
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

