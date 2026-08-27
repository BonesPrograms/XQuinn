using System.Reflection;
using XQ.Extensions;
using System.Text.RegularExpressions;
using XQ.Reflection;
using XQ.Parsing;
using XQ.CodeAnalysis.AST;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using XQ.CodeAnalysis;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;

namespace XQ.Runtime
{


    internal sealed class Navigator
    {




        /// <summary>
        /// Optional, primarily for use with dynamicinvoker so you do not need to use the typecache.
        /// </summary>
        public IReadOnlyDictionary<string, Type>? LocalCache;  ///This is not an actual read only wrapper, it is only a cast so that it can support being passed an IReadOnlyDictionary.
        public readonly InvocationLexer Lexer = new();

        /// <summary>
        /// A dictionary of all stored instances.
        /// </summary>
       // public readonly IReadOnlyDictionary<string, Variable> _variables;
        internal readonly Dictionary<string, VariableBinding> _variables = new(StringComparer.OrdinalIgnoreCase);


        /// <summary>
        /// The currently loaded instance.
        /// </summary>
        public object? LoadedInstance => _instance;
        object? _instance;
        public string? LoadedVariable => _variableKey;
        string? _variableKey;
        public Type? InstanceType => _instanceType;
        Type? _instanceType; //used for polymorphism checks

        /// <summary>
        /// The currently loaded type.
        /// </summary>
        public Type? LoadedType => _loadedType;
        Type? _loadedType;
        public string? LoadedTypeKey => _key;
        string? _key;
        internal Dictionary<string, MethodBase> _methods = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<ResolvedOverload, MethodBase> _overloads = new();
        internal readonly Dictionary<string, FieldInfo> _fields = new(StringComparer.OrdinalIgnoreCase);



        /// <summary>
        /// A dictionary of all overload methods found in the current loaded type, incase you are having trouble figuring out an overload's changed name. You can also use
        /// the MapOverloads method to see how it is resolved.
        /// </summary>
        //    public readonly IReadOnlyDictionary<string, MethodInfo> Overloads;


        /// <summary>
        /// This represents the most recently invoked method.
        /// </summary>
        public MethodBase? LoadedMethod => _loadedMethod;
        MethodBase? _loadedMethod;
        ParameterInfo[]? _loadedParams;
        internal static readonly Dictionary<Type, HashSet<string>> AmbiguousMatches = new();
        internal static readonly Dictionary<Type, Dictionary<string, MemberInfo>> KnownMembers = new(); //all members ever accessed by callinterp 
        internal static readonly Dictionary<string, Type> ReifiedGenerics = new(StringComparer.OrdinalIgnoreCase); //reified generics

        /// <summary>
        /// Caching adds methods and fields to a global cache as they are invoked by CallInterpreter.
        /// </summary>
        public bool Caching = true;
        //this dictionary keeps track of ambiguous matches for method names per type, allows minor efficiency upgrade, we dont always have to map out an entire type if the method is not overloaded
        const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        public Navigator()
        {

        }
        public Navigator(IReadOnlyDictionary<string, Type>? localCache = null)
        {
            LocalCache = localCache;
            //   Overloads = new ReadOnlyDictionary<string, MethodInfo>(_overloads);
            //Variables = new ReadOnlyDictionary<string, Variable>(_variables);
        }
        public static void FlushStaticCache(bool ambiguousMatches = false, bool typeMembers = true, bool reifiedGenerics = true)
        {
            if (ambiguousMatches)
                AmbiguousMatches.Clear(); //technically not part of 'caching' system, doesnt care if caching is false, will always store ambiguous matches
            if (typeMembers)                    //may be a thing later, for now you can clear at least
                KnownMembers.Clear();
            if (reifiedGenerics)
                ReifiedGenerics.Clear();
        }

        public List<string> ChainInvoke(params string[] commands)
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
                    sb.AppendLine($"!!! EXCEPTION on [Invocation: {cmd}]");
                    sb.CatchException(ex);
                    invocations.Add(sb.ToString());
                    return invocations;

                }
            }
            return invocations;
        }


        public void Clear()
        {

            _variables.Clear();
            _overloads.Clear();
            _methods.Clear();
            _fields.Clear();
            LocalCache = null;
            _instance = null;
            _instanceType = null;
            _loadedType = null;
            _loadedMethod = null;
            _loadedParams = null;

        }
        #region Interface
        /// <summary>
        /// Primary method for interfacing with call interpreter's various features.
        /// </summary>
        /// <param name="invocation"></param>
        /// <param name="catchinvoke"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public object? Interface(string invocation)
        {
            if (invocation.Length < 2 || string.IsNullOrWhiteSpace(invocation))
                return "No command detected.";
            return invocation[0] switch
            {
                '+' => AddViarable(invocation.Substring(1)), ///Add the current Instance to the Variables dictionary. Returns null.
                '-' => RemoveVariable(invocation.Substring(1)), ///Remove an instance from the Variables dictionary. Returns null.
               // '$' => LoadVariable(invocation.Substring(1)), ///Load an instance from the Variables dictionary as the current Instance. Returns loaded value.

                '@' => LoadTypeStatics(invocation.Substring(1)), ///Load a type's static members. Returns null.
                '*' => LoadInstance(invocation.Substring(1)), ///Load the current Instance from a field or method. Returns loaded value.
                '^' => CastInstance(invocation.Substring(1)), ///Cast the current Instance to a different type. Returns null.

                '!' => ExplicitInvoke(invocation.Substring(1)), ///Invoke a method or field by type name without changing the loaded type. Returns invoked value.
                '~' => ChainInvoke(invocation.Substring(1).Split(';'
#if NET6_0_OR_GREATER
                , StringSplitOptions.TrimEntries
#endif
                )), //chain calls                                             ///Can exclude type name to automatically invoke from the loaded type. This is how you view the values of fields, standard invoke
                /// will throw for anything except method invocations. Also allows you to invoke private members from base types without changing the loaded type.
                _ => StandardInvokeOrAssign(invocation) ///Invoke a method from the loaded type, or assign. Returns invoked value if invocation. Returns assigned value if assignment.
            };
        }
        #endregion




        #region Loading

        /// <summary>
        /// Load a new type by string. Must be cached in localcache or typecache. Resets loaded method, instance and type.
        /// </summary>
        /// <param name="typeName"></param>

        public Type LoadTypeStatics(string typeName)
        {
            TypeString tstring = TypeString.New(typeName);
            Type t = FindType(tstring, true);
            _key = tstring.String;
            LoadTypeMembers(t);
            _instance = null;
            _instanceType = null;
            _variableKey = null;
            return t;
        }

        /// <summary>
        /// Load a method directly. Must be static. Does not reset loaded type or instance.
        /// </summary>
        /// <param name="method"></param>
        /// <exception cref="ArgumentException"></exception>
        public void LoadMethodDirectly(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (!SupportedMember(method, parameters) || !method.IsStatic) throw new NotSupportedException($"Method {method} has in/out/or ref params, or is nonstatic, or has ref return type.");
            _loadedMethod = method;
            _loadedParams = parameters;
        }


        void LoadInstance(object instance, Type instanceType)
        {
            _instance = instance;
            LoadTypeMembers(instanceType);
            _instanceType = instanceType;
            _key = "this"; //We cannot know a the typecache key of a loaded instance, so the system defaults to "this" for implicit access of the loaded instance's type members
        }
        void LoadTypeMembers(Type type)
        {
            _loadedType = type;
            _loadedParams = null;
            _loadedMethod = null;
            MapType();
        }


        //Begins the cycle - lexes the invocation, loads the method, and then sends data off for parsing.
        object?[]? LoadInvocation(string invocation)
        {
            MethodString main = Lexer.ParameterTemplate(invocation, null); //at this point we cannot know if the generic arguments are the same yet, but incase youre reloading the same method with diff generic parameters, we always reload if we detect generic vs generic
            if (LoadedType == null)
                throw new InvalidOperationException("Must load a type before attempting to invoke.");
            _loadedMethod = FindMethod(main);
            _loadedParams = _loadedMethod.GetParameters();
            return GetParsedParameters(_loadedParams, main);
        }
        #endregion
        //If the method input name is equal to the loaded method name, this implies it is a generic method, since we do not allow overloads
        //We always reload on generics, since I cant know at this point what the types of your generic parameters are, they are just strings
        //Technically speaking if you invoke the same generic method with the same type parameters twice, it will recreate it both times, which is less efficient
        //But currently I dont care, I may fix that later

        #region Parsing

        /// <summary>
        /// Convert a method AST object to actual parameters by matching it to a MethodInfo's parameter array.
        /// </summary>
        public object?[] GetParsedParameters(ParameterInfo[] actualParameters, MethodString invocation)
        {
            object?[] prms = new object[actualParameters.Length];
            int inputAmount = invocation.Params.Count;
            int reqAmount = actualParameters.Length;
            int lastparam = reqAmount - 1;
            if (invocation.Params.Count != reqAmount)
                ResolveUnequalParameters(inputAmount, reqAmount, actualParameters, prms, invocation, lastparam);
            else if (lastparam >= 0 && actualParameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
                GetParamArray(lastparam, actualParameters, prms, invocation);
            else
                for (int i = 0; i < actualParameters.Length; i++)
                    prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
            return prms;
        }

        void ResolveUnequalParameters(int inputAmount, int reqAmount, ParameterInfo[] actualParameters, object?[] prms, MethodString invocation, int lastparam)
        {
            if (inputAmount < reqAmount)
            {
                for (int i = inputAmount; i < reqAmount; i++)
                {
                    ParameterInfo parameter = actualParameters[i];
                    if (parameter.HasDefaultValue)
                        prms[i] = parameter.DefaultValue;
                    else if (parameter.IsDefined(typeof(ParamArrayAttribute)))
                    {
                        Type elementType = actualParameters[lastparam].ParameterType.GetElementType() ?? throw new ArgumentNullException();
                        prms[i] = Array.CreateInstance(elementType, 0);
                    }
                    else
                        throw new TargetParameterCountException($"Parameter {parameter} does not have a default value. Input param count {inputAmount} Required count {reqAmount} method name {invocation.String}");
                }
                for (int i = 0; i < inputAmount; i++) prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
            }
            else
                if (lastparam >= 0 && actualParameters[lastparam].IsDefined(typeof(ParamArrayAttribute)))
                    GetParamArray(lastparam, actualParameters, prms, invocation);
                else
                    throw new TargetParameterCountException($"input param count: {invocation.Params.Count} required count: {actualParameters.Length} method name {invocation.String}");
        }

        void GetParamArray(int lastparam, ParameterInfo[] actualParameters, object?[] prms, MethodString invocation)
        {
            int lastBeforeThat = lastparam - 1;
            if (lastBeforeThat >= 0)
                for (int i = 0; i < lastBeforeThat; i++)
                    prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
            Type elementType = actualParameters[lastparam].ParameterType.GetElementType() ?? throw new ArgumentNullException();
            Array paramArray = Array.CreateInstance(elementType, invocation.Params.Count - lastparam); //assign array type via reflection becuase object[] wont work for params keyword
            for (int i = lastparam; i < invocation.Params.Count; i++)
                paramArray.SetValue(ParameterToObject(invocation.Params[i], elementType), i - lastparam);
            prms[lastparam] = paramArray;
        }
        //This sorts between whether or not a parameter is a method invocation as a parameter, or an actual primitive/string value.
        object? ParameterToObject(ParameterString value, Type paramType)
        {
            object? obj;
            if (value is FieldString field)
                obj = GetFieldWithVariable(field);
            else if (value is MethodString method)
                obj = InvokeMethodWithVariable(method);
            else
            {
                obj = ParseParameter((ValueString)value, paramType);
                if (obj == null && !(paramType.IsClass || (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>))))
                    throw new ArgumentException($"Expected method syntax for type {paramType}, but received {value.String}");
            }
            return obj;

        }


        object? ParseParameter(ValueString value, Type paramType) //need half and int128 support
        {
            string strng = value.String;
            if (strng.EqualsCaseless("this"))
            {
                if (LoadedInstance == null)
                    throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");
                if (!paramType.IsAssignableFrom(_instanceType))
                    throw new InvalidCastException($"Current instance is a {_instanceType} and does not cast to parameter type {paramType}");
                return LoadedInstance;
            }
            if (_variables?.TryGetValue(strng, out VariableBinding? variable) ?? false)
            {
                if (!paramType.IsAssignableFrom(variable.ObjectType))
                    throw new InvalidCastException($"Instance index object with key {strng} is a {variable.ObjectType} and does not cast to parameter type {paramType}");
                else return variable.Object;
            }
            if (_fields.TryGetValue(strng, out FieldInfo? field))
                return field.GetValue(_instance);
            return value.ParseValue(paramType);
        }



        object? GetFieldParameter(Type fromType, FieldString fieldstring, object? variable)
        {
            string fname = fieldstring.String;
            FieldInfo? field = CheckGlobalCache<FieldInfo>(fieldstring.String, fromType, out bool typeCached, out bool fieldCached);
            if (field == null && fromType == LoadedType)
                _fields.TryGetValue(fname, out field);
            else if (field == null)
                field = fromType.GetField(fname, Flag);
            if (field == null)
                throw new MissingFieldException($"No field found named {fname} in {fromType}.");
            CacheMember(typeCached, fieldCached, fromType, field, fieldstring.String);
            return field.GetValue(GetVariableInstance(fromType, variable));
        }
        object? InvokeMethodParameter(MethodString mthdString, Type fromType, object? variable)
        {
            MethodBase? call = CheckGlobalCache<MethodBase>(mthdString.NameWithGenerics, fromType, out bool typeCached, out bool methodCached);
            ParameterInfo[]? parameters = null;
            if (call == null && fromType != LoadedType)
            {
                Type cachedType = fromType;
                if (fromType.IsGenericType)
                    cachedType = fromType.GetGenericTypeDefinition();
                AmbiguousMatches.TryGetValue(cachedType, out HashSet<string>? cachedMatches);
                bool ambiguousMatch = cachedMatches?.Contains(mthdString.String) ?? false;
                if (!ambiguousMatch)
                {
                    try
                    {
                        call = fromType.GetMethod(mthdString.String, Flag);
                    }
                    catch (AmbiguousMatchException)
                    {
                        if (cachedMatches == null) { cachedMatches = new(StringComparer.OrdinalIgnoreCase); AmbiguousMatches[cachedType] = cachedMatches; }
                        cachedMatches.Add(mthdString.String);
                        // throw new AmbiguousMatchException($"Method named {mthdString.String} in type {fromType} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
                    }
                }
                if (call == null || ambiguousMatch)
                {
                    ResolvedOverload query = ResolvedOverload.OverloadQuery(mthdString);
                    List<MethodBase> methodbases = new();
                    MethodInfo[] methods = fromType.GetMethods(Flag);
                    for (int x = 0; x < methods.Length; x++)
                        if (methods[x].GetCustomAttribute(typeof(CompilerGeneratedAttribute)) == null)
                            methodbases.Add(methods[x]);
                    if (query.MethodKey.EqualsCaseless("new"))
                    {
                        ConstructorInfo[] ctors = fromType.GetConstructors(Flag);
                        for (int x = 0; x < ctors.Length; x++)
                            if (ctors[x].GetCustomAttribute(typeof(CompilerGeneratedAttribute)) == null)
                                methodbases.Add(ctors[x]);
                    }
                    int i = 0;
                    foreach (MethodBase method in methodbases)
                    {
                        string evaluatedName = method.Name == ".ctor" ? "new" : method.Name;
                        ParameterInfo[] methodparams = method.GetParameters();
                        if (SupportedMember(method, methodparams) && evaluatedName.EqualsCaseless(query.MethodKey))
                        {
                            if (i == query.Index)
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
            call ??= fromType == LoadedType ? FindMethod(mthdString) : throw new MissingMethodException($"No method named {mthdString.String} found in {fromType}'s methods or overload resolutions.");
            if (call.IsGenericMethodDefinition && call is MethodInfo mthd)
                call = mthdString.ConvertToGeneric(mthd, LocalCache);
            if (parameters == null)
            {
                parameters = call.GetParameters();
                if (!methodCached)
                {
                    if (fromType != LoadedType && !SupportedMember(call, parameters))
                        throw new ArgumentException($"Method {call} in type {call.DeclaringType} has unsupported in out or ref params or ref returntype");
                    CacheMember(typeCached, methodCached, fromType, call, mthdString.NameWithGenerics);
                }

            }
            object? obj = null;
            object?[]? parsedparams = GetParsedParameters(parameters, mthdString);
            try
            {
                obj = call is ConstructorInfo ctor ? ctor.Invoke(parsedparams) : call.Invoke(GetVariableInstance(fromType, variable), parsedparams);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            return obj;
            //if you are getting a type that you know will be in a metadata map, it doesnt need to have case //but if the map is null, then you need full casing  //namespace is ALWAYS required
        }


        object? GetVariableInstance(Type paramType, object? variable)
        {
            return variable ?? (paramType.IsAssignableFrom(_instanceType) ? LoadedInstance : null);
        }
        object? InvokeMethodWithVariable(MethodString method)
        {
            Type t = FindTypeWithVariable(method, out object? variable);
            return InvokeMethodParameter(method, t, variable);
        }
        object? GetFieldWithVariable(FieldString field)
        {
            Type t = FindTypeWithVariable(field, out object? variable);
            return GetFieldParameter(t, field, variable);
        }
        #endregion

        #region Finding

        /// <summary>
        /// Checks if a member is supported by Call Interpreter.
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        public static bool SupportedMember(MethodBase method, ParameterInfo[] parameters)
        {
            if (method is MethodInfo mthd && mthd.ReturnType.IsByRef)
                return false;
            return !parameters.Any(x => x.IsOut || x.IsIn || x.ParameterType.IsByRef);
        }

        Type FindTypeWithVariable(IMemberString member, out object? instance)
        {
            //  if (string.IsNullOrWhiteSpace(member.DeclaringType?.String)) { variable = new("this", _instance); return LoadedType ?? throw new InvalidOperationException("cannot implicitly access loaded type, no type is loaded"); }
            if (member.DeclaringType == null)
                throw new ArgumentNullException();
            if (_fields.TryGetValue(member.DeclaringType.String, out FieldInfo? field))
            {
                instance = field.GetValue(_instance); //?? throw new ArgumentException($"Field {field} in type {field.DeclaringType} returned null and it's field type's methods and fields cannot be invoked.");
                return field.FieldType;
            }
            if (_variables.TryGetValue(member.DeclaringType.String, out VariableBinding? variable))
            {
                instance = variable.Object;
                return variable.ObjectType;

            }
            instance = null;
            return FindType(member.DeclaringType, false);
        }

        Type FindType(TypeString typename, bool staticLoadOrCasting)
        {
            if (!staticLoadOrCasting)
            {
                if (_variables.TryGetValue(typename.String, out VariableBinding? variable))
                    return variable.ObjectType;
                if (typename.String.EqualsCaseless("this"))
                    return _instanceType ?? throw new InvalidOperationException("Cannot pass this, instance is null.");
                if (typename.String.EqualsCaseless(_key))
                    return LoadedType ?? throw new InvalidOperationException("Loaded Key is equal to input string, but loaded type is null? Report this to the developer.");
            }
            if (typename.String.EqualsCaseless("base"))
            {
                if (_instanceType == null)
                    throw new InvalidOperationException("Cannot get instance base, instance is null.");
                else return _instanceType.BaseType ?? throw new ArgumentException("Base type of instance is null.");
            }
            Type t = TypeCache.GetTypeOrThrow(typename.String, LocalCache);
            if (t.IsGenericTypeDefinition)
            {
                if (ReifiedGenerics.TryGetValue(typename.NameWithGenerics, out Type? generic))
                    return generic;
                t = typename.ConvertToGeneric(t, LocalCache);
                if (Caching)
                    ReifiedGenerics[typename.NameWithGenerics] = t;
            }
            return t;

        }

        MethodBase FindMethod(MethodString method)// out ResolvedOverload? query)
        {
            MethodBase? methodbase = null;
            _methods?.TryGetValue(method.String, out methodbase);
            if (methodbase == null)
            {
                ResolvedOverload query = ResolvedOverload.OverloadQuery(method);
                _overloads.TryGetValue(query, out methodbase);
            }
            if (methodbase == null)
                throw new MissingMethodException($"No method named {method.String} found in {LoadedType}'s method or overload dictionary. It may have been removed due to having a ref return type or in/out/ref parameters.");
            if (methodbase.IsGenericMethodDefinition && methodbase is MethodInfo actualmethod)
                methodbase = method.ConvertToGeneric(actualmethod, LocalCache);
            return methodbase;
        }

        static T? CheckGlobalCache<T>(string key, Type fromType, out bool typeCached, out bool memberCached) where T : MemberInfo
        {
            memberCached = false;
            typeCached = false;
            typeCached = KnownMembers.TryGetValue(fromType, out var cachedMembers);
            if (typeCached)
            {
                memberCached = cachedMembers!.TryGetValue(key, out MemberInfo? member);
                if (memberCached) return (T)member!;
            }
            return null;
        }
        void CacheMember(bool typeCached, bool memberCached, Type fromType, MemberInfo member, string key) //ResolvedOverload? overloadKey)
        {
            if (Caching)
            {
                Dictionary<string, MemberInfo> cachedMembers;
                if (!typeCached)
                {
                    cachedMembers = new(StringComparer.OrdinalIgnoreCase); KnownMembers[fromType] = cachedMembers;
                }
                else
                    cachedMembers = KnownMembers[fromType];
                if (!memberCached)
                    cachedMembers[key] = member;
                // if (fromType == LoadedType)
                // {
                //     if (member is FieldInfo field) _fields.Remove(key);
                //     else if (member is MethodInfo mthd) { _methods.Remove(key);}
                //     if(overloadKey!=null) _overloads.Remove(overloadKey.Value);
                //     Console.WriteLine($"{overloadKey!=null} OVERLOADKEY REMOVED?");
                // }
            }
        }


        #endregion

        #region Mapping


        static string? ResolveMemberAccess(string invocation, out string member, out bool field) //returns typename, outputs the accessed member
        {
            int? lastAccessorIndex = null;
            int paramStart = invocation.IndexOf('(');
            field = paramStart == -1;
            for (int i = 0; i < invocation.Length; i++) //this resolves typenames vs member names, lexer does something similar but not exactly the same - this one is pretty much universal
            {                                        //works with anything like field or method() (no type name) or namespace.typename.method(22, "hello", othertype.method()) //methods are broken off from the typename with all their parameters included
                if (i == paramStart) break;
                if (invocation[i] == '.') lastAccessorIndex = i;
            }
            if (lastAccessorIndex != null)
            {
                member = invocation.Substring(lastAccessorIndex.Value + 1);
                string typename = invocation.Remove(lastAccessorIndex.Value);
                return typename;
            }
            else
                member = invocation;
            return null; //no type name, just a member, this technically isnt allowed but i let you get away with it for instance loading 
        }

        void MapType()
        {
            _overloads.Clear();
            _methods.Clear();
            List<MethodBase> methodbases = LoadedType!.GetMethods(Flag)
            .Where(x => SupportedMember(x, x.GetParameters()) && x.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
            .Cast<MethodBase>()
            .ToList();
            IEnumerable<ConstructorInfo> ctors = LoadedType!.GetConstructors(Flag)
            .Where(x => SupportedMember(x, x.GetParameters()) && x.GetCustomAttribute<CompilerGeneratedAttribute>() == null);
            foreach (ConstructorInfo ctor in ctors)
                methodbases.Add(ctor);
            AddBackwards(_methods, methodbases);
            IEnumerable<string> names = methodbases
            .Select(x => x.Name == ".ctor" ? "new" : x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
                CompareNames(methodbases, name);
            _fields.Clear();
            AddBackwards(_fields, LoadedType!.GetFields(Flag));


        }

        static void AddBackwards<T>(Dictionary<string, T> storage, IList<T> memberArray) where T : MemberInfo
        {
            for (int i = memberArray.Count - 1; i >= 0; i--)
            {
                T member = memberArray[i];
                storage[member.Name == ".ctor" ? "new" : member.Name] = member;
            }
        }
        //this is a 'new' resolution thing
        //If you hide an inherited member with 'new', GetField/GetMethod will automatically get the newest member, but GetFields/GetMethods will also return hidden members as well
        //However, the newest member is closest to the 0 index in the info array relative to the older members
        //so functionally the 'older'members are overriden by doing a backwards for loop
        //you can explicitly access those members using casting syntax

        //There is a small issue with "new" resolution - new members are still currently added to the overload dictionary. Currently dont know how I will fix that yet, since it is hard
        //to differentiate overloads and new methods across inheritance hierarchies.

        //Also dictionary overwiting helps get rid of every overload except the first declared overload

        void CompareNames(List<MethodBase> methods, string name)
        {
            int count = 0;
            MethodBase? first = null;
            string? firstEvaluatedName = null;
            foreach (MethodBase method in methods)
            {
                string evaluatedName = method.Name == ".ctor" ? "new" : method.Name;
                if (name.Equals(evaluatedName, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                    if (count == 1)
                    {
                        first = method;
                        firstEvaluatedName = evaluatedName;
                    }
                    else
                    {
                        _overloads[new(evaluatedName, count - 1)] = method;
                        if (first != null)
                        {
                            _overloads[new(firstEvaluatedName!, 0)] = first;
                            _methods.Remove(firstEvaluatedName!);
                            first = null;
                            firstEvaluatedName = null;
                        }
                    }

                }

            }
        }

        #endregion

        #region Interfacing


        //Checks for method or field syntax. If it detects a method, it diverts to an isolated type load and method invocation.
        object LoadInstance(string invocation)
        {
            object? instance = null;
            Type? objectType = null;
            _variableKey = null;
            if (_variables.TryGetValue(invocation, out VariableBinding? variable))
            {
                _variableKey = invocation;
                instance = variable.Object;
                objectType = variable.ObjectType;
            }
            instance ??= ExplicitInvoke(invocation) ?? throw new ArgumentException($"Failed to load new instance from {invocation}, invocation returned null!");
            objectType ??= instance.GetType();
            LoadInstance(instance, objectType);
            return variable ?? instance;
        }
        //Isolated lexing, loading and invocation for "quick invocation" without resetting loaded instance, method or type.
        object? ExplicitInvoke(string invocation)
        {
            string typeName = ResolveMemberAccess(invocation, out string member, out bool field) ?? _key ?? throw new ArgumentException($"No type loaded to return fields from, or no type name given for isolated invocation.");
            if (!field)
                return InvokeMethodWithVariable(Lexer.ParameterTemplate(member, typeName!));
            else
            {
                if (member.EqualsCaseless("this"))
                    return _instance ?? "null";
                if (_variables.TryGetValue(member, out VariableBinding? variable))
                    return variable;
                return GetFieldWithVariable(new(member, TypeString.New(typeName)));
            }

        }
        Type CastInstance(string invocation)
        {
            if (LoadedInstance == null)
                throw new InvalidOperationException("Cannot cast, instance is null.");
            TypeString tstring = TypeString.New(invocation);
            Type t = FindType(tstring, true);
            _key = tstring.String;
            if (!t.IsAssignableFrom(_instanceType))
                throw new InvalidCastException($"{_instanceType} cannot cast to {t}.");
            LoadTypeMembers(t);
            return t;
        }

        object? StandardInvokeOrAssign(string invocation)
        {
            if (Assignment(invocation, out object? assigned))
                return assigned;
            object? returned = null;
            object?[]? parameters = LoadInvocation(invocation);
            try
            {
                returned = LoadedMethod is ConstructorInfo ctor ? ctor.Invoke(parameters) : LoadedMethod!.Invoke(LoadedInstance, parameters);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            return returned;
        }

        bool Assignment(string invocation, out object? assignedValue)
        {
            assignedValue = null;
            string[] assignment = invocation.Split('=');
            if (assignment.Length == 1) return false;
            if (assignment.Length != 2) throw new ArgumentException($"Invalid assignment, can only contain left hand and right hand. Bad assignment: {invocation}");
            string lefthand = assignment[0];
            string righthand = assignment[1];
            string? lefthandTypeName = ResolveMemberAccess(lefthand, out lefthand, out bool lefthandfield);
            if (!lefthandfield) throw new ArgumentException($"Can only assign to fields. Bad input: {lefthand}");
            Type lefthandtype;
            object? instance;
            lefthand = lefthand.Trim();
            if (lefthandTypeName == null)
            {
                lefthandtype = LoadedType ?? throw new ArgumentException($"There is no loaded type to assign fields to. Bad input: {invocation}");
                instance = LoadedInstance;
            }
            else
            {
                lefthandtype = FindTypeWithVariable(new FieldString(lefthand, TypeString.New(lefthandTypeName)), out object? variable);
                instance = variable;
            }
            FieldInfo assigningTo = lefthandtype.GetField(lefthand, Flag) ?? throw new MissingFieldException($"No field found in type {lefthandtype} named {lefthand}");
            string? righthandTypeName = ResolveMemberAccess(righthand, out righthand, out bool righthandfield);// ?? _key ?? throw new ArgumentException("No type loaded for implicit access on righthand side.");
            if (righthandTypeName != null) assignedValue = righthandfield ? GetFieldWithVariable(new(righthand, TypeString.New(righthandTypeName))) : InvokeMethodWithVariable(Lexer.ParameterTemplate(righthand, righthandTypeName));
            else assignedValue = ParseParameter(new(righthand.Trim()), assigningTo.FieldType);
            assigningTo.SetValue(instance, assignedValue);
            return true;
        }

        bool RemoveVariable(string key)
        {

            if (key.EqualsCaseless(_variableKey))
                _variableKey = null;
            return _variables.Remove(key);
        }

        bool AddViarable(string key)
        {
            if (LoadedInstance == null)
                throw new InvalidOperationException("No instance is loaded.");
            TypeCache.ThrowIfBadKey(key);
            Type? typeWithConflictingKey = TypeCache.GetType(key, LocalCache);
            if (typeWithConflictingKey != null)
                throw new ArgumentException($"Key {key} is already taken by a cached type, and cannot be used as a name for a local variable. Names are not case sensitive.");
            if (_variables.TryGetValue(key, out VariableBinding? variable))
            {
                if (!ReferenceEquals(LoadedInstance, variable.Object))
                    throw new ArgumentException("Duplicate keyname detected.");
                return false;
            }
            _variables[key] = new(LoadedInstance, _instanceType!);
            _variableKey = key;
            return true;
        }


        // VariableBinding LoadVariable(string key)
        // {
        //     if (_variables.Count == 0) throw new ArgumentException("No instances are currently stored.");
        //     if (!_variables.TryGetValue(key, out VariableBinding? variable)) throw new ArgumentException($"There is no stored instance with the key {key}.");
        //     _variableKey = key;
        //     LoadInstance(variable.Object, variable.ObjectType);
        //     return variable;
        // }
        #endregion



        internal sealed class VariableBinding
        {
            public readonly object Object;
            public readonly Type ObjectType;
            internal VariableBinding(object instance, Type instanceType)
            {
                Object = instance;
                ObjectType = instanceType;
            }

            public override string ToString()
            {
                return $"ObjectType: {ObjectType} :: ObjectToString: {Object}";
            }
        }

        internal readonly struct ResolvedOverload : IEquatable<ResolvedOverload>
        {
            public readonly int Index;
            public readonly string MethodKey;
            internal ResolvedOverload(string name, int index)
            {
                Index = index;
                int last = name.IndexOf(':');
                MethodKey = last != -1 ? name.Remove(last) : name;
            }

            internal static ResolvedOverload OverloadQuery(MethodString mthdString)
            {
                string name = mthdString.String;
                string[] query = name.Split(':');
                int index = 0;
                if (query.Length == 2)
                    index = int.Parse(query[1]);
                else if (query.Length != 1)
                    throw new FormatException($"Invalid overload query. {mthdString.String} Proper Format: Name:Index");
                return new(mthdString.String, index);
            }

            bool IEquatable<ResolvedOverload>.Equals(ResolvedOverload obj)
            {
                return Equals(obj);
            }
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(MethodKey);//StringComparer.FromComparison(StringComparison.OrdinalIgnoreCase).GetHashCode(KeySpan);
                    hash = hash * 31 + Index.GetHashCode();
                    return hash;
                }
            }
            public override string ToString() => Index > 0 ? $"{MethodKey}:{Index}" : MethodKey;
            public bool Equals(ResolvedOverload overload)
            {
                return overload.Index == Index && overload.MethodKey.Equals(MethodKey, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object? obj)
            {
                return obj is ResolvedOverload overload && Equals(overload);
            } //obj is Overload overload && ((IEquatable<Overload>)this).Equals(overload);

            public static bool operator ==(ResolvedOverload left, ResolvedOverload right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(ResolvedOverload left, ResolvedOverload right)
            {
                return !(left == right);
            }
        }



    }

}
