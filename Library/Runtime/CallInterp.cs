using System.Reflection;
using XQuinn.Extensions;
using System.Text.RegularExpressions;
using XQuinn.Reflection;
using XQuinn.Parsing;
using XQuinn.Parsing.AST;
using XQuinn.NetConsole;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;

namespace XQuinn.Runtime
{
    /// <summary>
    /// Wrapper around an exception thrown by a method invoked via reflection, primarily to prevent memory leakage from hot-reloaded assemblies.
    /// </summary>
    public class CapturedException : Exception
    {
        public CapturedException(MethodInfo LoadedMethod, Exception? inner) : base($"Method {LoadedMethod!.Name} threw exception: {inner?.GetType().FullName} {inner?.Message} {inner?.StackTrace}")
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



        //Loading Communicators: Begin your string with these characters to load
        // * Load an instance from a Static Field or Method (you do not need to load a type first to load an instance from a static field or method)
        // _ Load an instance from an Instance field or Method
        // ! Load a type (statics only)

        //Method Invocation Communicators:
        // ( Parameters Begin
        // ) Parameters End
        // , Parameter End
        // . Member Access (only goes one layer. IE. TypeName.FieldName, not TypeName.FieldName.MethodName)
        // " String Declaration (opening and closing)
        // ' Char Declaration (opening and closing) digits and letters technically don't need char declaration enclosures, but i recommend to use them habitually
        // \ Escape Sequence (for strings, chars dont need escapes in this interpreter)
        // _ Instance Access (preceding a Member Access Communicator)

        //What is Loading?
        //Loading stores a type's fields and methods within the Call Interpreter. Once a type is loaded, you can invoke it's methods by name.
        //All public, nonpublic and inherited fields and methods available to the loaded type will be accessible.

        //Loading Use:

        //Load Type (static):
        //!TypeName

        //Load instance via static: begin string with an * asterisk
        //*TypeName.Field | load an static field as the current instance
        //*TypeName.Method() | return an instance from a static method

        //Load instance via instance: After loading an instance via a static field or method, you can load from it's instance fields and methods. Begin string with an _ underscore.
        //#this.Field | load an instance field as the current instance.
        //#this.Method() | return an instance from an instance method

        //CALLING METHODS:
        //Once a type or instance is loaded, you invoke methods by string name using standard C# syntax, ie.
        //DoMath(22, 23) or Print<string>("hello","world")
        //Parameters of non-primitive types and enums such as user-defined structs and classes (besides strings) must be returned via method or field.

        //Passing methods and fields as parameters:

        //If accessing a static method or field for a parameter, its syntax is similar to static loading, but without an asterisk. Example:
        // Type.ReturnValue()
        //If accessing an instance method or field for a parameter, it's syntax is a mix of static and instance loading. Example:
        // this.ReturnValue() would access the instance method ReturnValue()

        //Example of returning a user-defined struct or class as a parameter with static calls:
        //ReadObject(GameHelpers.GetObject()) for a method or ReadObject(GameCore.Player) for a field 
        //Instance example:
        //ReadObject(_.GetInfo()) for a methoid and ReadObject(_.Player) for a field

        //WHITESPACE:
        //Whitespace functions just like C#. It is usually skipped but once you begin a declaration we stop skipping it and if we read whitespace in the middle of a declaration,
        //the lexer will throw an exception.

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
        //      void OtherMethod();
        //      void OtherMethod(string);
        //  }

        //Stored names and methods:

        //Method1 - void Method()
        //Method2 - void Method(int)
        //OtherMethod1 - void OtherMethod()
        //OtherMethod2 - void OtherMethod(string)


        //NEW/HIDING:
        //When hiding an inherited method with the New keyword, the original method will still be accessible - the names will be resolved similarly to overloads.
        //The new method in the inheritor class will be counted first, and the base classes hidden method will be counted second. IE.
        //
        //class BaseClass
        //{
        //public void Num();
        //}
        //
        //class Inheritor : BaseClass
        //{
        //new void Num()
        //}
        //
        //
        //Stored names and methods
        //Num1 - Inheritor.Num()
        //Num2 - BaseClass.Num()

        //Overload name resolution can get pretty confusing to predict as you include more and more inheritance or hiding. If you're having trouble, make a MemberMap of your chosen type
        //and use MapOverloads to see how it resolves the method keys.s

        //I should note: Methods are sorted by static and instance before being sorted by overloads, so if you have a mix of static and instance overloads, the instance methods won't be part of the overload
        //count if you are loading static only, so it may be harder to decipher count just by reading your file top to bottom.

        //INSTANCE INDEX:
        //You can store the currently loaded instance in a caseless string key dictionary for reloading later on. Begin your string with these Communicators:
        // + adds the loaded instance by key. Example: +MyObject
        // - removes an instance by key. Ex. -MyObject
        // $ loads an instance by key. Ex. $MyObject

        //BASE PRIVATE MEMBERS: ACCESS WITH EXPLICIT SYNTAX
        //When you have an instance loaded, you can explicitly access private base members of that instance by leading with the base type's name, similar to static syntax.
        //This works for both parameters, and loading syntax.
        //For loading, you would lead with the basetype's name, 
        //ex. #BaseType.Field
        //For parameters, instead of accessing by this, you would access it by type name, again, similar to static syntax
        //ex. BaseType.Method()

        //But wait, I want to *invoke* base private methods as the loaded method!
        //To do that, you can use the casting feature.
        // ^TypeName to cast the current instance to a differnt type.



        //Overloads in base types are resolved the same way.

        //KNOWN BUGS

        //1 If you have a method that takes an int? parameter and you return the value using a method that has an int return type, you will get an exception saying
        //the return type doesnt match the parameter type. 


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
        public object? Instance => _instance;
        object? _instance;
        Type? InstanceType;

        /// <summary>
        /// The currently loaded type from which you can invoke static methods.
        /// </summary>
        public Type? LoadedType => _loadedtype;
        Type? _loadedtype;
        MemberMap? Map;

        /// <summary>
        /// This represents the most recently invoked and currently loaded method.
        /// </summary>
        public MethodInfo? LoadedMethod => _loadedMethod;

        MethodInfo? _loadedMethod;

        ParameterInfo[]? LoadedParameters;

        //for staging- load a type before calling

        static readonly HashSet<string> AmbiguousMatches = new(StringComparer.OrdinalIgnoreCase);

        HashSet<string>? LoadedAmbiguousMatches;

        public const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // HashSet<string>? LoadedTypeAmbiguousMatches = new(StringComparer.OrdinalIgnoreCase);
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
            _loadedMethod = null;
            LoadedParameters = null;

        }

        /// <summary>
        /// Interface with CallInterpreter. Returns the loaded instance after loading, or the returned value of a method after invoking.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="catchinvoke"></param>
        /// <returns></returns>
        /// 



        public object? Interface(string input, bool catchinvoke = false)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or whitespace.");
            return input[0] switch
            {
                '+' => AddInstance(input.Substring(1)),
                '-' => RemoveInstance(input.Substring(1)),
                '$' => GetInstance(input.Substring(1)),
                '!' => LoadTypeClearInstance(input.Substring(1)),
                '*' => StaticLoad(input.Substring(1)),
                '#' => InstanceLoad(input.Substring(1)),
                '^' => Cast(input.Substring(1)),
                _ => Invoke(input, catchinvoke)
            };
        }

        /// <summary>
        /// Convert a method AST object to actual parameters by matching it to a MethodInfo's parameter array.
        /// </summary>
        public object?[]? GetParams(ParameterInfo[] methodparams, Method method)
        {
            object?[]? prms = null;
            if (method.Params.Count != methodparams.Length)
                throw new TargetParameterCountException($"input param count: {method.Params.Count} required count: {methodparams.Length} method name {method.String}");
            if (methodparams.Length > 0)
            {
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
            _instance = obj;
            _loadedtype = obj.GetType();
            InstanceType = _loadedtype;
            ChangeTMap();
        }

        /// <summary>
        /// Load a new type by string. Must be cached in book or typecache. Resets loaded method, instance and type.
        /// </summary>
        /// <param name="typeName"></param>

        public object? LoadTypeClearInstance(string typeName)
        {
            Type t = FindType(new(typeName));
            LoadTypePreserveInstance(t);
            _instance = null;
            InstanceType = null;
            return null;
        }

        void LoadTypePreserveInstance(Type t)
        {

            _loadedtype = t;
            ChangeTMap();
        }

        /// <summary>
        /// Load a method directly. Must be static. Does not reset loaded type or instance.
        /// </summary>
        /// <param name="method"></param>
        /// <exception cref="ArgumentException"></exception>
        public void LoadMethodDirect(MethodInfo method)
        {
            if (!SupportedMember(method) || !method.IsStatic)
                throw new NotSupportedException($"Method {method} has delegate, in/out/or ref params, or is nonstatic.");
            _loadedMethod = method;
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
                throw new CapturedException(LoadedMethod!, inner);
            }
        }
        ///Checks for method or field syntax. If it detects a method, it runs the entire CallInterpreter like normal and assigns the returned value.
        object? InstanceLoad(string input)
        {
            if (Instance == null)
                throw new InvalidOperationException("Cannot load from instance, instance is null.");
            string? tname = ResolveMemberAccess(input, out string member, out bool field);
            Type t = tname == null ? LoadedType! : FindType(new(tname)); ;
            object? instance;
            if (t != LoadedType)
                LoadTypePreserveInstance(t);
            if (!field)
            {
                instance = Invoke(member);
            }
            else
                instance = GetFieldParameter(t, member, tname ?? LoadedType!.FullName!);
            if (instance == null)
                throw new ArgumentException($"Load returned null, unable to load instance. Input: {input}");
            DirectLoadInstance(instance);
            return Instance;
        }


        //Checks for method or field syntax. If it detects a method, it diverts to an isolated type load and method invocation.
        object? StaticLoad(string input)
        {
            string tname = ResolveMemberAccess(input, out string member, out bool field) ?? throw new ArgumentException($"Cannot static load without a type name. Bad input: {input}");
            Type type = FindType(new(tname, null));
            object? instance;
            if (!field)
                instance = StaticInvoke(member, type);
            else
            {
                try
                {
                    instance = type.InvokeMember(member, Flag | BindingFlags.GetField, null, null, null);
                }
                catch (MissingFieldException)//the exception for this is bugged, and does not actually display the input field name, it instead displays an irrelevent method name
                {
                    throw new MissingFieldException($"Could not find field  \"{member}\" in {type.FullName}");
                }
            }
            if (instance == null)
                throw new ArgumentException($"Failed to load new instance from {input}");
            DirectLoadInstance(instance);
            return Instance;
        }
        //An isolated lexing, loading and invocation for static loading.
        object? StaticInvoke(string input, Type type)
        {
            Method main = Lexer.ParameterTemplate(input);
            var map = MapType(type, MemberGroup.Method, out Dictionary<string, MethodInfo>? overloads);
            var method = FindMethod(overloads, map, main, main.String, type.Name);
            var param = GetParams(method.GetParameters(), main);
            return method.Invoke(null, param);
        }

        void LoadMethod(Method mthd)
        {
            if (LoadedType == null)
                throw new InvalidOperationException("Must load a type before attempting to invoke.");
            if (LoadedAmbiguousMatches?.Contains(mthd.String) ?? false)
                throw new AmbiguousMatchException($"Method named {mthd.String} in type {LoadedType} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
            MethodInfo method = FindMethod(_overloads, Map!, mthd, mthd.String, LoadedType!.FullName!);
            _loadedMethod = method;
            LoadedParameters = method.GetParameters();
        }



        void ChangeTMap()
        {
            LoadedParameters = null;
            _loadedMethod = null;
            Map = MapType(LoadedType!, MemberGroup.Method | MemberGroup.Field, out Dictionary<string, MethodInfo>? overloads);
            if (_overloads != null)
                _overloadsreadonly = new ReadOnlyDictionary<string, MethodInfo>(_overloads);
            else
                _overloadsreadonly = null;
            _overloads = overloads;
            if (overloads != null)
            {
                LoadedAmbiguousMatches?.Clear();
                LoadedAmbiguousMatches ??= new(StringComparer.OrdinalIgnoreCase);
                foreach (var method in overloads.Values)
                    LoadedAmbiguousMatches.Add(method.Name);
            }
            else
                LoadedAmbiguousMatches = null;
        }

        //Begins the cycle - lexes the invocation, loads the method, and then sends data off for parsing.
        object?[]? LoadInvocation(string invocation)
        {
            Method main = Lexer.ParameterTemplate(invocation); //at this point we cannot know if the generic arguments are the same yet, but incase youre reloading the same method with diff generic parameters, we always reload if we detect generic vs generic
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
                obj = GetFieldParameter(FindType(f._type), f.String, f.DeclaringType);
            else if (param is Method m)
                obj = InvokeMethodParameter(m);
            else
            {
                obj = ParseParameter(param.String, paramType);
                if (obj == null)
                {       //This should never return null except for Nullable<T> and String, it will always throw a Format exception otherwise because the only other things it parses are primitives
                    bool validNullableInput = param.String == "null" && (paramType.IsClass || paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>));
                    if (!validNullableInput)
                        throw new ArgumentException($"Expected method syntax for type {paramType}, but received {param.String}");
                }       //so if its not throwing format exception, it means it is a class or custom struct parameter (it leaves early and avoids throwing), and it requires method syntax, however it should've already jumped to the Method label early, this means it was parsed incorrectly as a Parameter, and will throw to let you know you messed up
            }
            return obj;

        }

        object? GetFieldParameter(Type t, string fname, string fdeclr)
        {

            FieldInfo field;
            if (t == LoadedType)
                field = FindField(Map!, fname, fdeclr);
            else
            {
                field = t.GetField(fname, Flag) ?? throw new MissingFieldException($"No field found in type {t} named {fname}");
                if (!SupportedMember(field))
                    throw new NotSupportedException($"Delegates are unsupported. Bad object: {field.DeclaringType}.{field.Name}");
            }
            //var map = MapType(t, MemberGroup.Field, out _);
            //FieldInfo field = FindField(map, f);
            //  if (!Convertible(field.FieldType, paramType))
            //    throw new ArgumentException($"Field {field.DeclaringType}{f.String} is type {field.FieldType}, but the parameter type required for method {LoadedMethod!.Name} is {paramType}");
            object? obj;
            if (t.IsAssignableFrom(LoadedType) && Instance != null)
                obj = field.GetValue(Instance);
            else
                obj = field.GetValue(null);
            return obj;
        }

        //An isolated invocation of a method, however unlike StaticInvoke, this one actually changes its available methods depending on whether or not your parameters
        //are actually instance methods in the currently loaded instance.
        object? InvokeMethodParameter(Method method)
        {
            Type t = FindType(method._type!);
            MethodInfo? call = null;
            if (t != LoadedType)
            {
                string key = $"{t.FullName}.{method.String}";
                if (!AmbiguousMatches.Contains(key))
                {
                    try
                    {
                        call = t.GetMethod(method.String, Flag);
                    }
                    catch (AmbiguousMatchException)
                    {

                        AmbiguousMatches.Add(key);
                        throw new AmbiguousMatchException($"Method named {method.String} in type {t} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
                    }
                }
                else
                    throw new AmbiguousMatchException($"Method named {method.String} in type {t} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
            }
            if (call == null) //this will throw if it cannot resolve the method
            {
                MemberMap tmap = MapType(t, MemberGroup.Method, out Dictionary<string, MethodInfo>? overloads);
                call = FindMethod(overloads, tmap, method, method.String, method.DeclaringType!);
            }
            //    if (!Convertible(call.ReturnType, paramType))
            //     throw new ArgumentException($"Method {method.DeclaringType}.{method.String} has a return type of {call.ReturnType}, but the parameter type required for method {LoadedMethod!.Name} is {paramType}");
            ParameterInfo[] methodparams = call.GetParameters();
            object?[]? subparams = GetParams(methodparams, method);
            object? instance = t.IsAssignableFrom(LoadedType) && Instance != null ? Instance : null;
            return call.Invoke(instance, subparams);
            //if you are getting a type that you know will be in a metadata map, it doesnt need to have case //but if the map is null, then you need full casing  //namespace is ALWAYS required
        }

        //this causes truncation, so for now it isnt a thing...
        // static bool Convertible(Type t, Type target)
        // {
        //     TypeConverter converter = TypeDescriptor.GetConverter(t);
        //     return converter.CanConvertTo(target);
        // }
        ///so this is why the "efficiency" getmethod thing doesnt work
        /// if you have two overloads named Integer()
        /// you will send Integer(), it will throw ambiguous match, then it will sort the overloads by count
        /// //however your string is still Integer() so itll never find it, and if you dont throw an ambiguous match, then it wont do the count
        /// so yeah no thing


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
            Type mainType = InstanceType ?? LoadedType!;
            if (strng == "this")
            {
                if (Instance == null)
                    throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");
                if (type.IsAssignableFrom(mainType))
                    return Instance;
                else
                    throw new FormatException($"{InstanceType} does not cast to {type}");
            }
            if (InstanceIndex?.TryGetValue(strng, out object? instance) ?? false)
            {
                Type indexedtype = instance.GetType();
                if (indexedtype.IsAssignableFrom(mainType))
                    return instance;
                else
                    throw new FormatException($"{indexedtype} does not cast to {type}");
            }
            if (type.IsClass && (strng == "null" || type != typeof(string)))
                return null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) //ValueType of Nullable<T>
            {
                if (strng == "null")
                    return null;
                Type? underlying = Nullable.GetUnderlyingType(type);
                if (underlying != null)
                    return ParseParameter(strng, underlying);
                else
                    throw new ArgumentException($"Underlying type for Nullable<T> type {type} is null.");
            }
            if (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
                return null; //user defined struct
#if NET6_0_OR_GREATER
            if (type.IsEnum && Enum.TryParse(type, strng, out object? enumeration))
                return enumeration;
#else
            if (type.IsEnum)
            {
                MethodInfo method = typeof(Enum).GetMethods().First(x => x.Name == "TryParse" && x.GetParameters().Length == 2);
                method = method.MakeGenericMethod(type);
                object? parsed = null;
                method.Invoke(null, new object?[] { strng, parsed });
                if (parsed != null)
                    return parsed;
            }
#endif
            if (type == typeof(string))//&& strng.Length >= 2 && strng[0] == StringDeclr && strng[^1] == StringDeclr)
            {
                const string regex = "^\"(.*?)\"$";// "\"([^\"]*)\""; //this new one supports fuckery like this "hello "world"" so itll pop out as hello "world" :)
                var matches = Regex.Match(strng, regex);
                if (matches.Success)
                    return matches.Groups[1].Value;
                else
                    throw new FormatException($"Non-null string values must be surrounded with quotations, even empty strings. Bad input: {strng}");
            }

#if NET6_0_OR_GREATER
            if (type == typeof(nint) && nint.TryParse(strng, out nint nativeint))
                return nativeint;
            if (type == typeof(nuint) && nuint.TryParse(strng, out nuint nativeuint))
                return nativeuint;
#endif
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
            if ((type == typeof(decimal)) && decimal.TryParse(strng, out decimal dec))
                return dec;
            throw new FormatException($"Tried to parse {strng} to {type}, but value could not parse to {type}.");
        }

        static FieldInfo FindField(MemberMap tmap, string field, string fielddeclr)
        {
            tmap.TryGetValue(MemberGroup.Field, out List<MemberInfo>? fields);
            if (fields == null)
                throw new InvalidOperationException($"No field were loaded from {fielddeclr}.");
            FieldInfo realfield = fields.FirstOrDefault(x => x.Name.EqualsCaseless(field)) as FieldInfo ?? throw new MissingFieldException($"No field found named {field} in {fielddeclr}. Delegate type fields are not supported and would have been removed.");
            return realfield;
        }

        //there is a another suble "parameterless" related bug, harmless, this one with generic parameters
        //if your method is nongeneric, you can input 10,000 generic params, it will parse them, but totally ignore them and invoke your method just fine

        //Finds a method by name in a membermap or overload dictionary, and converts it to a generic method if the AST method object itself has generic parameters.
        MethodInfo FindMethod(Dictionary<string, MethodInfo>? overloads, MemberMap tmap, Method method, string name, string tname)
        {
            tmap.TryGetValue(MemberGroup.Method, out List<MemberInfo>? methods);
            if (methods == null)
                throw new InvalidOperationException($"No method were loaded from {tname}. They were removed due to invalid parameter types.");
            MethodInfo? realmethod = null;
            overloads?.TryGetValue(method.String, out realmethod);
            realmethod ??= methods.FirstOrDefault(x => x.Name.EqualsCaseless(name)) as MethodInfo ?? throw new MissingMethodException($"No method named {name} found in {tname}, or the method was removed from the typemap due to having invalid parameters (delegate in out or ref)");
            if (realmethod.IsGenericMethodDefinition)
                realmethod = ASTToGeneric(realmethod.MakeGenericMethod, method);

            return realmethod;
        }

        /// <summary>
        /// Convert a generic definition to a constructed generic with a GenericParameter AST object.
        /// </summary>
        /// <typeparam name="M"></typeparam>
        /// <typeparam name="P"></typeparam>
        /// <param name="genericConstructor"></param>
        /// <param name="genericAST"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public M ASTToGeneric<M, P>(Func<Type[], M> genericConstructor, P genericAST) where M : MemberInfo where P : GenericParameter
        {
            Type[] generics = GetGenerics(genericAST) ?? throw new ArgumentException($"{genericAST.GetType().Name} {genericAST.String} is generic definition, but no generic parameters were provided.");
            return genericConstructor.Invoke(generics);
        }



        ///Gets generic string arguments from a Method AST object.
        public Type[]? GetGenerics(GenericParameter p) => p.Generics?.Select(FindType).ToArray();

        //Finds types in a book or the main cache, converts them to generic based on the generic parameters of the TypeString AST object.
        public virtual Type FindType(TypeString strng)
        {
            if (strng.String == "this")
            {
                if (Instance == null)
                    throw new InvalidOperationException("Cannot invoke from instance as parameter, instance is null.");
                return LoadedType!;
            }
            Type t = TypeCache.GetTypeOrThrow(strng.String, Book);
            if (t.IsGenericTypeDefinition)
                t = ASTToGeneric(t.MakeGenericType, strng);
            return t;
        }

        //Gets a membermap (or returns the currently loaded one) that is sorted for supported methods, then sorts out any overloads.
        MemberMap MapType(Type type, MemberGroup group, out Dictionary<string, MethodInfo>? overloads)
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
            //   bool instanced = type.IsAssignableFrom(LoadedType) && Instance != null; //this allows you to access private base members in an instance explicitly
            MemberMap tmap = MemberMap.New(type, false, false, false, group, SupportedMember);
            // // if (group.HasFlag(MemberGroup.Method))
            //  {
            try
            {
                overloads = tmap.MapOverloads(out List<MemberInfo>? methods, StringComparer.OrdinalIgnoreCase);
                if (methods != null)
                {
                    tmap[MemberGroup.Method] = methods;
                }
            }
            catch (InvalidOperationException)
            {
                // throw new InvalidOperationException($"No methods were loaded from the type {type.Name}. They may have been removed if they do not meet the criteria, such as trying to load instance methods without loading an instance, or trying to use a method with in out ref or delegate parameters.");
            }
            //  }
            return tmap;

        }


        /// <summary>
        /// Checks if a method is supported by Call Interpreter.
        /// </summary>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static bool SupportedMember(MemberInfo mem)
        {
            if (mem is MethodInfo meth)
            {
                var parameters = meth.GetParameters();
                if (parameters.Any(x => x.IsOut || x.IsIn || x.ParameterType.IsByRef || typeof(Delegate).IsAssignableFrom(x.ParameterType)))
                    return false;
                return true;

            }
            else if (mem is FieldInfo f)
                return !typeof(Delegate).IsAssignableFrom(f.FieldType);
            return false;
        }

        object? Cast(string input)
        {
            if (Instance == null)
                throw new InvalidOperationException("Cannot cast, instance is null.");
            TypeString typeString = new(input);
            Type t = FindType(typeString);
            if (t.IsGenericTypeDefinition)
                t = ASTToGeneric(t.MakeGenericType, typeString);
            if (!t.IsAssignableFrom(InstanceType))
                throw new InvalidCastException($"{InstanceType} cannot cast to {t}.");
            LoadTypePreserveInstance(t);
            return null;
        }

        object? Invoke(string input, bool catchinvoke)
        {
            if (!catchinvoke)
                return Invoke(input);
            CatchInvoke(input);
            return null;
        }


        object? AddInstance(string key)
        {
            if (Instance == null)
                throw new InvalidOperationException("No instance is loaded.");
            _instanceIndex ??= new(StringComparer.OrdinalIgnoreCase);
            _instanceindexreadonly ??= new ReadOnlyDictionary<string, object>(_instanceIndex);
            if (_instanceIndex.TryGetValue(key, out object? val))
            {
                if (!ReferenceEquals(val, Instance))
                    throw new ArgumentException("Duplicate keyname detected.");
            }
            _instanceIndex[key] = Instance;
            return null;
        }

        object? RemoveInstance(string key)
        {
            if (_instanceIndex == null)
                throw new InvalidOperationException("No instances are currently stored.");
            _instanceIndex.Remove(key);
            if (_instanceIndex.Count == 0)
            {
                _instanceindexreadonly = null;
                _instanceIndex = null;
            }
            return null;

        }

        object GetInstance(string key)
        {
            if (_instanceIndex == null)
                throw new InvalidOperationException("No instances are currently stored.");
            if (!_instanceIndex.TryGetValue(key, out object? instance))
                throw new ArgumentException($"There is no stored instance with the key {key}.");
            DirectLoadInstance(instance);
            return instance;
        }


        public static string? ResolveMemberAccess(string input, out string member, out bool field) //returns typename, outputs the accessed member
        {
            int? lastAccessorIndex = null;
            int paramStart = input.IndexOf('(');
            field = paramStart == -1;
            for (int i = 0; i < input.Length; i++) //this resolves typenames vs member names, lexer does something similar but not exactly the same - this one is pretty much universal
            {                                           //works with anything like field or method() (no type name) or namespace.typename.method(22, "hello", othertype.method()) 
                if (input[i] == '.')                       //methods are broken off from the typename with all their parameters included
                    lastAccessorIndex = i;
                else if (i == paramStart)
                    break;

            }
            if (lastAccessorIndex != null)
            {
                member = input.Substring(lastAccessorIndex.Value + 1);
                string typename = input.Remove(lastAccessorIndex.Value);
                return typename;
            }
            member = input;
            return null; //no type name, just a member
        }

    }

}