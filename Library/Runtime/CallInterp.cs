using System.Reflection;
using XQuinn.Extensions;
using System.Text.RegularExpressions;
using XQuinn.Reflection;
using XQuinn.Parsing;
using XQuinn.Parsing.AST;

namespace XQuinn.Runtime;
/// <summary>
/// Wrapper around an exception thrown by a method invoked via reflection, primarily to prevent memory leakage from hot-reloaded assemblies.
/// </summary>
public class ReflectedMethodException : Exception
{
    public ReflectedMethodException(MethodInfo LoadedMethod, Exception? inner) : base($"Method {LoadedMethod!.Name} threw exception: {inner?.GetType().FullName} {inner?.Message} {inner?.StackTrace}")
    {

    }
}

//i may one day decouple

/// <summary>
/// Runtime debugging tool that can load the methods and fields of static types and instances by string input, allowing one to invoke methods directly in objects at runtime. Comes
/// with a suite of other debug capabilities like storing and reloading instances.
/// </summary>
public class CallInterpreter
{



    //Loading Operators: Begin your string with these characters to load
    // * Load an instance from a Static Field or Method (you do not need to load a type first to load an instance from a static field or method)
    // _ Load an instance from an Instance field or Method
    // ! Load a type (statics only)

    //Calling Operators:
    // ( Parameters Begin
    // ) Parameters End
    // , Parameter End
    // : Static Access

    //Loading Use:

    //Load Type (static):
    //!TypeName

    //Load instance via static: begin string with an * asterisk
    //*TypeName:Field | load an static field as the current instance
    //*TypeName:Method() | return an instance from a static method

    //Load instance via instance: After loading an instance via a static field or method, you can load from it's instance fields and methods. Begin string with an _ underscore.
    //_Field | load an instance field as the current instance.
    //_Method() | return an instance from an instance method



    //CALLING METHODS:
    //Once a type or instance is loaded, you invoke methods by string name using mostly standard C# syntax, ie.
    //DoMath(22, 23) or Print<string>("hello","world") notice the quotes, strings require quotes. Also yes, this supports generics, either generic types or methods.
    //Parameters of non-primitive types and enums such as user-defined structs and classes (besides strings) must be returned via method or field.

    //passing methods and fields as parameters:

    //If accessing a static method or field for a parameter, its syntax is similar to static loading, but without an asterisk. Example:
    // Type:ReturnValue()
    //If accessing an instance method or field for a parameter, it's syntax is a mix of static and instance loading. Example:
    // _:ReturnValue() would access the instance method ReturnValue()

    //Example of returning a user-defined struct or class as a parameter with static calls:
    //ReadObject(GameHelpers:GetObject()) for a method or ReadObject(GameCore:Player) for a field 
    //Instance example:
    //ReadObject(_:GetInfo()) for a methoid and ReadObject(_:Player) for a field

    //UNSUPPORTED METHODS:
    //In, out and ref parameters are not supported. Methods with delegate type parameters are not supported.


    //UNSUPPORTED TYPES:
    //Nested generic parameters are not yet supported. IE. a method like MakeList<List<string>>() will not parse
    //Ive never used dynamic types, and thus have never tested CallInterpreter with it. I may try to see how that works later.
    //This system relies on TypeCache, your types will not be found if they are not registered with the cache. If you don't want to register with the cache, you can
    //create your own TypeBook for the Interpreter to retrieve types from. By default, typebooks are not case sensitive. Call Interpreter itself is not case sensitive.
    //Whether or not full type names with namespaces are required can be decided by you when making an instance of a TypeBook.

    //Primitive types, string and object are already supported and cached by the TypeCache.

    //OVERLOADS:
    //Overload names are stored with their name and number count. The counter reads from top to bottom, ie. the overload that is declared closest to the top
    //of your actual type file will get the number 1, and so on. For example, it will look like this
    //
    //  class Type
    //  {
    //      void Method();
    //      void Method(int);
    //  }

    //Stored names and methods:

    //Method1 - void Method()
    //Method2 - void Method(int)

    //I should note: Methods are sorted by static and instance before being sorted by overloads, so if you have a mix of static and instance overloads, the instance methods won't be part of the overload
    //count if you are loading static only, so it may be harder to decipher count just by reading your file top to bottom.

    //INSTANCE INDEX:
    //You can store the currently loaded instance in a caseless string key dictionary for reloading later on. Syntax is simple.
    //After an instance is loaded,
    // + adds the instance by key. Example: +MyObject
    // - removes an instance by key. Ex. -MyObject
    // $ loads an instance by key. Ex. $MyObject


    //KNOWN BUGS

    //1 if you are terminating a method being used as a parameter and follow it with a comma, it will count the whitespace in between as a parameter. Ex.
    // MethodCall(22, MyType:MyMethod(), "hello")
    //The 0-length string between ) and , after Mymethod() will count as a parameter and will cause a parameter count mismatch

    //2 If you have a method that takes an int? parameter and you return the value using a method that has an int return type, you will get an exception saying
    //the return type doesnt match the parameter type. 

    //3 Whitespace will cause the null value to fail to parse. Example
    // Right way : Call(22,null,15)
    //Wrong Way: Call(22, null,15)
    //Will fix this later

    //4. Whitespace will also cause type and method names to fail to parse. Generally good to just avoid whitespace all together. Will fix this later.


    /// <summary>
    /// Optional, this is a map of all the types in your module for caching. Primarily for use with
    /// dynamicinvoker so you dont need to cache in the global typecache and potentially
    /// cause memleaks and keyname conflicts when reloading and re-caching
    /// </summary>
    public TypeBook? Book;

    /// <summary>
    /// Turns strings into ASTs.
    /// </summary>
    public readonly InvocationLexer Lexer = new();
    /// <summary>
    /// A dictionary of all overload methods found in the current loaded type or instance.
    /// </summary>
    public IReadOnlyDictionary<string, MethodInfo>? Overloads => _overloadsreadonly;
    IReadOnlyDictionary<string, MethodInfo>? _overloadsreadonly;
    Dictionary<string, MethodInfo>? _overloads;

    /// <summary>
    /// A dictionary of all stored instances.
    /// </summary>
    public IReadOnlyDictionary<string, object>? InstanceIndex => _instanceindexreadonly;
    IReadOnlyDictionary<string, object>? _instanceindexreadonly;
    Dictionary<string, object>? _instanceIndex;
    /// <summary>
    /// The currently loaded instance from which you can invoke instance and static methods.
    /// </summary>
    ///
    public object? Instance { get => _instance; private set => _instance = value; }
    object? _instance;

    /// <summary>
    /// The currently loaded type from which you can invoke static methods.
    /// </summary>
    public Type? LoadedType { get => _loadedtype; private set => _loadedtype = value; }
    Type? _loadedtype;
    MemberMap? Map;

    /// <summary>
    /// This represents the most recently invoked and currently loaded method.
    /// </summary>
    public MethodInfo? LoadedMethod { get => _loadedMethod; private set => _loadedMethod = value; }

    MethodInfo? _loadedMethod;

    ParameterInfo[]? LoadedParameters;

    //for staging- load a type before calling
    public CallInterpreter(TypeBook? book = null)
    {
        Book = book;
    }

    public void Clear()
    {
        //sszLexer.Clear();
        _instanceindexreadonly = null;
        _instanceIndex = null;
        _overloadsreadonly = null;
        _overloads = null;
        Book = null;
        _instance = null;
        _loadedtype = null;
        Map = null;
        LoadedMethod = null;
        LoadedParameters = null;

    }

    /// <summary>
    /// Interface with CallInterpreter. Returns the loaded instance after loading, or the returned value of a method after invoking.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="catchinvoke"></param>
    /// <returns></returns>
    public object? Interface(string input, bool catchinvoke = false)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(input);
        if (input[0] == '+')
        {
            if (Instance == null)
                throw new ArgumentException("No instance is loaded.");
            string key = input.Substring(1);
            _instanceIndex ??= new(StringComparer.OrdinalIgnoreCase);
            _instanceindexreadonly ??= _instanceIndex.AsReadOnly();
            if (_instanceIndex.TryGetValue(key, out object? val))
            {
                if (!ReferenceEquals(val, Instance))
                    throw new ArgumentException("Duplicate keyname detected.");
                return Instance;
            }
            _instanceIndex[key] = Instance;
            return Instance;
        }
        else if (input[0] == '-')
        {
            if (_instanceIndex == null)
                throw new ArgumentException("No instances are currently stored.");
            string key = input.Substring(1);
            _instanceIndex.Remove(key);
            if (_instanceIndex.Count == 0)
            {
                _instanceindexreadonly = null;
                _instanceIndex = null;
            }
            return null;
        }
        else if (input[0] == '$')
        {
            if (_instanceIndex == null)
                throw new ArgumentException("No instances are currently stored.");
            string key = input.Substring(1);
            if (_instanceIndex.TryGetValue(key, out object? instance))
                Instance = instance;
            else
                throw new ArgumentException($"There is no stored instance with the key {key}.");
            return Instance;
        }
        else if (input[0] == '!')
        {
            LoadTypeDirect(input.Substring(1));
            return null;
        }
        else if (input[0] == '*')
        {
            return StaticLoad(input.Substring(1));
        }
        else if (input[0] == '_')
        {
            return InstanceLoad(input.Substring(1));
        }
        else if (!catchinvoke)
            return Invoke(input);
        else
        {
            CatchInvoke(input);
            return null;
        }
    }

    /// <summary>
    /// Convert a method AST object to actual parameters by matching it to a MethodInfo's parameter array.
    /// </summary>
    /// <param name="methodparams"></param>
    /// <param name="method"></param>
    /// <returns></returns>
    /// <exception cref="TargetParameterCountException"></exception>
    public object?[]? GetParams(ParameterInfo[] methodparams, Method method)
    {
        object?[]? prms = null;
        if (methodparams.Length > 0)
        {
            if (method.Params.Count != methodparams.Length)
                throw new TargetParameterCountException($"input param count: {method.Params.Count} required count: {methodparams.Length} method name {method.String}");
            prms = new object[method.Params.Count];
            for (int i = 0; i < methodparams.Length; i++)
            {
                prms[i] = ParameterToObject(method.Params[i], methodparams[i].ParameterType);
            }
        }
        return prms;
    }
    /// <summary>
    /// Invoke from a string after loading a type or instance.
    /// </summary>
    /// <param name="invocation"></param>
    /// <returns></returns>
    public object? Invoke(string invocation)
    {
        object?[]? objs = LoadInvocation(invocation);
        //    if (LoadedMethod == null) //method laodiong is handle in createparams
        //       throw new InvalidOperationException("Loaded method is null.");
        return LoadedMethod!.Invoke(Instance, objs);
    }

    /// <summary>
    /// Load an instance directly via object reference. Resets loaded method, instance and type.
    /// </summary>
    /// <param name="obj"></param>
    public void DirectLoadInstance(object obj)
    {
        Instance = obj;
        LoadTypePreserveInstance(obj.GetType());
    }

    /// <summary>
    /// Load a new type by string. Must be cached in book or typecache. Resets loaded method, instance and type.
    /// </summary>
    /// <param name="typeName"></param>

    public void LoadTypeDirect(string typeName)
    {
        Type t = FindType(new(typeName));
        LoadTypeDirect(t);
    }

    /// <summary>
    /// Load a new type directly by object reference. Resets loaded method, instance and type.
    /// </summary>
    /// <param name="t"></param>
    public void LoadTypeDirect(Type t)
    {
        Instance = null;
        LoadedType = t;
        ChangeTMap();
    }

    /// <summary>
    /// Load a method directly. Must be static. Does not reset loaded type or instance.
    /// </summary>
    /// <param name="method"></param>
    /// <exception cref="ArgumentException"></exception>
    public void LoadMethodDirect(MethodInfo method)
    {
        if (!SupportedMethod(method) || !method.IsStatic)
            throw new ArgumentException($"Method {method} has delegate, in/out/or ref params, or is nonstatic.");
        LoadedMethod = method;
        LoadedParameters = method.GetParameters();
        //  LoadTypeDirect(method.DeclaringType!);
    }
    /// <summary>
    /// This is a very specific method for dynamic invoker that i will probably move over there soon. It catches outside exceptions to prevent them from "escaping" and causing memleaks.
    /// </summary>
    void CatchInvoke(string invocation)
    {
        object?[]? objs = LoadInvocation(invocation);
        try
        {
            LoadedMethod!.Invoke(null, objs); ;
        }
        catch (TargetInvocationException ex) //memory leak prevention, though im not sure if this is fully necessary
        {
            var inner = ex.InnerException;
            throw new ReflectedMethodException(LoadedMethod!, inner);
        }
    }
    ///Checks for method or field syntax. If it detects a method, it runs the entire CallInterpreter like normal and assigns the returned value.
    object? InstanceLoad(string input)
    {
        if (Instance == null)
            throw new ArgumentException("Cannot load from instance, instance is null.");
        object? instance;
        int length = input.Length;
        if (length > 2 && (input[length - 1] == ')'))
            instance = Invoke(input);
        else
            instance = LoadedType!.InvokeMember(input, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField, null, Instance, null);
        if (instance == null)
            throw new ArgumentException($"Load returned null, unable to load instance. Input: {input}");
        DirectLoadInstance(instance);
        return Instance;
    }

    //Checks for method or field syntax. If it detects a method, it diverts to an isolated type load and method invocation.
    object? StaticLoad(string input)
    {
        string[] inputs = input.Split(':');
        if (inputs.Length != 2)
            throw new ArgumentException($"static load input is invalid: {input}");
        Type type = FindType(new(inputs[0].Substring(0), null));
        object? instance;
        string invoc = inputs[1];
        int num = invoc.Length;
        if (num > 2 && (invoc[num - 1] == ')')) //method return
        {
            instance = StaticInvoke(inputs[1], type);
        }
        else //field
            instance = type.InvokeMember(inputs[1], BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField, null, null, null);
        if (instance == null)
            throw new ArgumentException($"Failed to load new instance from {input}");
        DirectLoadInstance(instance);
        return Instance;
    }
    //An isolated lexing, loading and invocation for static loading.
    object? StaticInvoke(string input, Type type)
    {
        Method main = Lexer.ParameterTemplate(input);
        var map = MapType(type, out Dictionary<string, MethodInfo>? overloads);
        var method = FindMethod(overloads, map, main, main.String, type.Name);
        var param = GetParams(method.GetParameters(), main);
        return method.Invoke(null, param);
    }

    void LoadMethod(Method mthd)
    {
        if (LoadedType == null)
            throw new InvalidOperationException("Must load a type before attempting to invoke.");
        MethodInfo method = FindMethod(_overloads, Map!, mthd, mthd.String, LoadedType!.FullName!);
        LoadedMethod = method;
        LoadedParameters = method.GetParameters();
    }

    void LoadTypePreserveInstance(Type type)
    {
        LoadedType = type;
        ChangeTMap();
    }

    void ChangeTMap()
    {
        LoadedParameters = null;
        LoadedMethod = null;
        Map = MapType(LoadedType!, out Dictionary<string, MethodInfo>? overloads);
        _overloadsreadonly = overloads?.AsReadOnly();
        _overloads = overloads;
    }

    //Begins the cycle - lexes the invocation, loads the method, and then sends data off for parsing.
    object?[]? LoadInvocation(string invocation)
    {
        Method main = Lexer.ParameterTemplate(invocation); //at this point we cannot know if the generic arguments are the same yet, but incase youre reloading the same method with diff generic parameters, we always reload if we detect generic vs generic
        if (main == null)
            throw new ArgumentException($"Unable to parse method or load from input. {invocation}");
        if (LoadedMethod == null || !LoadedMethod.Name.EqualsCaseless(main.String) || LoadedMethod.IsGenericMethod)// && main.IsGeneric)
            LoadMethod(main);
        //   if (LoadedParameters == null) //already handles whether or not your method loads in LoadMethod
        //     throw new InvalidOperationException("Must load a method before creating parameters.");
        return GetParams(LoadedParameters!, main);
    }

    //If the method input name is equal to the loaded method name, this implies it is a generic method, since we do not allow overloads
    //We always reload on generics, since I cant know at this point what the types of your generic parameters are, they are just strings
    //Technically speaking if you invoke the same generic method with the same type parameters twice, it will recreate it both times, which is less efficient
    //But currently I dont care, I may fix that later


    //This sorts between whether or not a parameter is a method invocation as a parameter, or an actual primitive/string value.
    object? ParameterToObject(Parameter param, Type paramType)
    {
        object? obj;
        if (param is Field f)
        {
            Type t = FindType(f._type);
            var map = MapType(t, out _);
            FieldInfo field = FindField(map, f);
            if (field.DeclaringType == LoadedType && Instance != null)
                return field.GetValue(Instance);
            else
                return field.GetValue(null);
        }
        else if (param is not Method)
        {
            obj = ParseParameter(param.String, paramType);
            if (obj == null)
            {       //This should never return null except for Nullable<T> and String, it will always throw a Format exception otherwise because the only other things it parses are primitives
                bool validNullableInput = param.String == "null" && (paramType.IsClass || paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>));
                if (!validNullableInput)
                    throw new ArgumentException($"Expected method syntax for type {paramType}, but received {param.String}");
            }       //so if its not throwing format exception, it means it is a class or custom struct parameter (it leaves early and avoids throwing), and it requires method syntax, however it should've already jumped to the Method label early, this means it was parsed incorrectly as a Parameter, and will throw to let you know you messed up
        }
        else
            obj = InvokeMethodParameter(param, paramType);
        return obj;

    }

    //An isolated invocation of a method, however unlike StaticInvoke, this one actually changes its available methods depending on whether or not your parameters
    //are actually instance methods in the currently loaded instance.
    object? InvokeMethodParameter(Parameter param, Type paramType)
    {
        Method method = (Method)param;
        Type t = FindType(method._type!);
        var tmap = MapType(t, out Dictionary<string, MethodInfo>? overloads);
        var realmethod = FindMethod(overloads, tmap, method, method.String, method.DeclaringType!);
        if (realmethod.ReturnType != paramType)
            throw new ArgumentException($"Method {method.DeclaringType}.{method.String} has a return type of {realmethod.ReturnType}, but the parameter type required for method {LoadedMethod!.Name} is {paramType}");
        ParameterInfo[] methodparams = realmethod.GetParameters();
        object?[]? subparams = GetParams(methodparams, method);
        object? instance = t == LoadedType ? Instance : null;
        return realmethod.Invoke(instance, subparams);
        //if you are getting a type that you know will be in a metadata map, it doesnt need to have case //but if the map is null, then you need full casing  //namespace is ALWAYS required
    }


    //there  is a suble bug here with parameterless methods
    //you can input 1 million parameters in your string input, it  will parse all of them too, but it wont
    //notify you what youre doing, will skip them, and just invoke
    //dont really give a shit right now since its harmless though i could fix that easily right here right now


    //internal for debugging purposes only, Invoc and its inheritors shant be exposed

    //Compared parameter types to string inputs, returning null for classes and user defined structs and otherwise matching primitives and strings. Can be overloaded
    //to allow you to return special inputs, for example you could have type GameObject and string "player" combination return a static field for the player in a game.
    //So far this is about the only overloadable thing.
    protected virtual object? ParseParameter(string strng, Type type) //need half and int128 support
    {

        //   if (strng.IsWhiteSpace()) //Should give more specific exceptions incase you are using an object parameter (it will ask for method syntax instead of sayingg it cant parse)
        //    goto Throw;
        if (type.IsClass && (strng == "null" || type != typeof(string)))
            return null;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) //ValueType of Nullable<T>
        {
            if (strng == "null")
                return null;
            Type? underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return ParseParameter(strng, underlying);
        }
        if (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
            return null; //user defined struct
        if (type.IsEnum && Enum.TryParse(type, strng, out object? enumeration))
            return enumeration;
        if (type == typeof(string))//&& strng.Length >= 2 && strng[0] == StringDeclr && strng[^1] == StringDeclr)
        {
            const string regex = "^\"(.*?)\"$";// "\"([^\"]*)\""; //this new one supports fuckery like this "hello "world"" so itll pop out as hello "world" :)
            var matches = Regex.Match(strng, regex);
            if (matches.Success)
                return matches.Groups[1].Value;
        }
        if (type == typeof(nint) && nint.TryParse(strng, out nint nativeint))
            return nativeint;
        if (type == typeof(nuint) && nuint.TryParse(strng, out nuint nativeuint))
            return nativeuint;
        if ((type == typeof(bool)) && bool.TryParse(strng, out bool boolean))
            return boolean;
        if ((type == typeof(char)) && char.TryParse(strng, out char utf16))
            return utf16;
        if ((type == typeof(byte)) && byte.TryParse(strng, out byte uint8))
            return uint8;
        if ((type == typeof(sbyte)) && sbyte.TryParse(strng, out sbyte sint8))
            return sint8;
        if ((type == typeof(short)) && short.TryParse(strng, out short sint16))
            return sint16;
        if ((type == typeof(ushort)) && ushort.TryParse(strng, out ushort uint16))
            return uint16;
        if ((type == typeof(int)) && int.TryParse(strng, out int sint32))
            return sint32;
        if ((type == typeof(uint)) && uint.TryParse(strng, out uint uint32))
            return uint32;
        if ((type == typeof(long)) && long.TryParse(strng, out long sint64))
            return sint64;
        if ((type == typeof(ulong)) && ulong.TryParse(strng, out ulong uint64))
            return uint64;
        if ((type == typeof(float)) && float.TryParse(strng, out float float32))
            return float32;
        if ((type == typeof(double)) && double.TryParse(strng, out double float64))
            return float64;
        throw new FormatException($"Tried to parse {strng} to {type}, but value could not parse to {type}.");
    }

    static FieldInfo FindField(MemberMap tmap, Field field)
    {
        tmap.TryGetValue(MemberGroup.Field, out List<MemberInfo>? fields);
        if (fields == null)
            throw new InvalidOperationException($"No field were lodaed from {field.DeclaringType}. It is likely they are instance fields in a statically loaded type.");
        FieldInfo realfield = tmap.FirstOrDefault(x => x.Name.EqualsCaseless(field.String)) as FieldInfo ?? throw new ArgumentException($"No field found named {field.String} in {field.DeclaringType}. It is possible it is an instance field, which are not available in static loads.");
        return realfield;
    }

    //there is a another suble "parameterless" related bug, harmless, this one with generic parameters
    //if your method is nongeneric, you can input 10,000 generic params, it will parse them, but totally ignore them and invoke your method just fine

    //Finds a method by name in a membermap or overload dictionary, and converts it to a generic method if the AST method object itself has generic parameters.
    MethodInfo FindMethod(Dictionary<string, MethodInfo>? overloads, MemberMap tmap, Method method, string name, string tname)
    {
        tmap.TryGetValue(MemberGroup.Method, out List<MemberInfo>? methods);
        if (methods == null)
            throw new InvalidOperationException($"No method were loaded from {tname}. They were likely removed due to invalid criteria, such as trying to load instance methods via a static type load, or due to having invalid parameter types.");
        MethodInfo? realmethod = null;
        overloads?.TryGetValue(method.String, out realmethod);
        realmethod ??= tmap.FirstOrDefault(x => x.Name.EqualsCaseless(name)) as MethodInfo ?? throw new ArgumentException($"No method named {name} found in {tname}, or the method was removed from the typemap due to having invalid parameters (delegate in out or ref), being nonstatic without having an instance loaded, or being an overloaded method.");
        if (realmethod.IsGenericMethodDefinition)
        {
            Type[]? generics = GetGenerics(method);
            if (generics == null)
                throw new ArgumentException($"Method;{realmethod} is generic definition, but no generic parameters were provided.");
            int req = realmethod.GetGenericArguments().Length;
            if (generics.Length != req)
                throw new ArgumentException($"generic input param count {generics.Length} required param count {req} for method {name}");
            realmethod = realmethod.MakeGenericMethod(generics); //this one throws on its own, checking generic constraints would be a pain in the fucking ass
        }
        return realmethod;
    }

    // void CompareConstraints(Type[] generics, MethodInfo realmethod)
    // {
    //     Type[] constraints = realmethod.GetG
    // }



    ///Gets generic string arguments from a Method AST object.
    public Type[]? GetGenerics(GenericParameter p) => p.Generics?.Select(x => FindType(new(x.String, null))).ToArray();

    //Finds types in a book or the main cache, converts them to generic based on the generic parameters of the TypeString AST object.
    public virtual Type FindType(TypeString strng)
    {
        if (strng.String == "_")
        {
            if (Instance == null)
                throw new ArgumentException("Cannot invoke from instance as parameter, instance is null.");
            return LoadedType!;
        }
        Type t = TypeCache.GetTypeOrThrow(strng.String, Book);
        if (t.IsGenericTypeDefinition)
        {
            Type[]? generics = GetGenerics(strng);
            if (generics == null)
                throw new ArgumentException($"Type {t} is generic definition, but no generic parameters were provided.");
            int req = t.GetGenericArguments().Length;
            if (generics.Length != req)
                throw new ArgumentException($"generic input param count {generics.Length} required param count {req} for type {strng.String}");
            t = t.MakeGenericType(generics);
        }
        return t;
    }

    //Gets a membermap (or returns the currently loaded one) that is sorted for supported methods, then sorts out any overloads.
    MemberMap MapType(Type type, out Dictionary<string, MethodInfo>? overloads)
    {
        if (type == LoadedType && Map?.Type == type)//Prevents recreating the same map, but needs to check instance vs static first
        {
            if (Instance == null && Map.StaticOnly) //incase you load a type via statics then load an instance of that type
            {
                overloads = _overloads;
                return Map;
            }
            else if (Instance != null && !Map.StaticOnly)
            {
                overloads = _overloads;
                return Map;
            }
        }
        overloads = null;
        bool instanced = type == LoadedType && Instance != null;
        MemberMap tmap = MemberMap.New(type, false, !instanced, false, MemberGroup.Method | MemberGroup.Field, x => SupportedMethod(x));
        try
        {
            overloads = tmap.MapOverloads(out List<MemberInfo>? methods, StringComparer.OrdinalIgnoreCase);
            if (methods != null)
                tmap[MemberGroup.Method] = methods;
        }
        catch (InvalidOperationException)
        {
            //  throw new InvalidOperationException($"No methods were loaded from the type {type.Name}. They may have been removed if they do not meet the criteria, such as trying to load instance methods without loading an instance, or trying to use a method with in out ref or delegate parameters.");
        }

        return tmap;

    }


    /// <summary>
    /// Checks if a method is supported by Call Interpreter.
    /// </summary>
    /// <param name="mem"></param>
    /// <returns></returns>
    public static bool SupportedMethod(MemberInfo mem)
    {
        if (mem is MethodInfo meth)
        {
            var parameters = meth.GetParameters();
            if (parameters.Any(x => x.IsOut || x.IsIn || x.ParameterType.IsByRef || typeof(Delegate).IsAssignableFrom(x.ParameterType)))
                return false;
            return true;

        }
        else if (mem is FieldInfo f)
            return true;
        return false;
    }










}

