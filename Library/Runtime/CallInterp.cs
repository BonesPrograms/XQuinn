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
using _xquinn_cor;

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
    public sealed class CallInterpreter
    {



        //Loading Communicators: Begin your string with these characters to load
        // * Load an instance from a Static Field or Method (you do not need to load a type first to load an instance from a static field or method)
        // # Load an instance from an Instance field or Method
        // @ Load a type (statics only)

        //Method Invocation Communicators:
        // ( Parameters Begin
        // ) Parameters End
        // , Parameter End
        // . Member Access (only goes one layer. IE. TypeName.FieldName, not TypeName.FieldName.MethodName)
        // " String Declaration (opening and closing)
        // ' Char Declaration (opening and closing) digits and letters technically don't need char declaration enclosures, but i recommend to use them habitually
        // \ Escape Sequence (for strings, chars dont need escapes in this interpreter)

        //What is Loading?
        //Loading stores a type's fields and methods within the Call Interpreter. Once a type is loaded, you can invoke it's methods by name.
        //All public, nonpublic and inherited fields and methods available to the loaded type will be accessible.

        //Loading Use:

        //Load Type (static):
        //@TypeName

        //Load instance via static: begin string with an * asterisk
        //*TypeName.Field | load an static field as the current instance
        //*TypeName.Method() | return an instance from a static method

        //Load instance via instance: After loading an instance via a static field or method, you can load from it's instance fields and methods using 'this'
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
        //ReadObject(this.GetInfo()) for a methoid and ReadObject(this.Player) for a field

        //WHITESPACE:
        //Whitespace functions just like C#. It is usually skipped but once you begin a declaration we stop skipping it and if we read whitespace in the middle of a declaration,
        //the lexer will throw an exception.

        //UNSUPPORTED:
        //In, out and ref parameters are not supported. Delegate type parameters are not supported.
        //Nested generic parameters are not yet supported. IE. a method like MakeList<List<string>>() will not lex
        //Ive never used dynamic types, and thus have never tested CallInterpreter with it. I may try to see how that works later.
        //This system relies on TypeCache, your types will not be found if they are not registered with the cache. If you don't want to register with the cache, you can
        //create your own Dictionary <string,Type> and assign it to "Book".
        //Call Interpreter is not case sensitive. You may experience some inconsistency if your dictionary is case sensitive.

        //Primitive types, string and object are already supported and cached by the TypeCache.

        //OVERLOADS:
        //Overload names are stored with their name and number count, except for the first overload. The counter reads from top to bottom, ie. the overload that is declared closest to the top
        //of your actual type file will be the first overload and have no number. For example, assume we are getting public declared and inherited methods from class InheritorType:
        //
        // class TopType
        //  {
        //     public void Method(char);
        //  }
        //  class BaseType : TopType
        // {
        //     public void Method(string);
        //     public void Method(long);
        //     public void OtherMethod(int);
        // }
        //  class InheritorType : BaseType
        //  {
        //      public void Method();
        //      public void Method(int);
        //      public void OtherMethod();
        //      public void OtherMethod(string);
        //  }

        //If you have multiple overloads across an inheritance hierarchy, the first multiple overloads will be selected from the type you are getting methods from, in our case, InheritorType
        //This continues up the chain, so types further up in the inheritance hierarchy
        // will have their overloads indexed further away from the 0 index, compared to overloads from types lower in the inheritance hierarchy,
        //which will be closer to the zero index.

        //Stored names and methods example:

        //Method - void Method()
        //Method1 - void Method(int)
        //Method2 - void Method(string)
        //Method3 - void Method(long)
        //Method4 - void Method(char)
        //OtherMethod - void OtherMethod()
        //OtherMethod1 - void OtherMethod(string)
        //OtherMethod2 - void OtherMethod(int)

        //NEW/HIDING:
        //If you are trying to access an inherited member that has been hidden with 'new', you can explicitly access hidden members using casting syntax



        //CASTING SYNTAX FOR INSTANCES:
        //When you have an instance loaded, you can explicitly access base members of that instance by leading with the base type's name, similar to static syntax.
        //This works for both parameters, and loading syntax.
        //For loading, you would lead with the basetype's name, instead of loading by this
        //ex. #BaseType.Field
        //For parameters, instead of accessing by this, you would access it by type name, again, similar to static syntax
        //ex. BaseType.Method()

        //This feature also works for accessing base members that have been hidden by the 'new' keyword.
        //It can also be used to access the "base" version of an override.
        //This also works the other way around, so when you cast to a base type, you can explicitly access members of the instance's actual type by leading with the instance's type name.

        //You can also optionally use the 'base' keyword, this will get the currently loaded type's basetype. ex.
        // base.Method()
        //Note that this is the *loaded* type, not the type of the instance.
        // Using casting with the ^ character, you can change loadedtype to be different from the instance's actual type.
        //IE. ^BaseTypeName
        //This reloads the immediately available fields and methods and changes the loadedtype, but maintains the instance.

        //Polymorphism remains the same as it normally does. If you cast an instance to a base type and attempt to invoke an overriden method, it will automatically invoke
        //the instance's override, even if it is getting the method from the base type.

        //INSTANCE INDEX:
        //You can store the currently loaded instance in a caseless string key dictionary for reloading later on. Begin your string with these Communicators:
        // + adds the loaded instance by key. Example: +MyObject
        // - removes an instance by key. Ex. -MyObject
        // $ loads an instance by key. Ex. $MyObject
        //Keys MUST only consist of alphanumeric characters and/or underscores. The first character of a key CANNOT be a digit.

        //KNOWN BUGS

        //1 If you have a method that takes an int? parameter and you return the value using a method that has an int return type, you will get an exception saying
        //the return type doesnt match the parameter type. 


        /// <summary>
        /// Optional, primarily for use with dynamicinvoker so you do not need to use the typecache.
        /// </summary>
        public IReadOnlyDictionary<string, Type>? LocalCache;

        ///This is not an actual read only wrapper, it is only a cast so that it can support being passed an IReadOnlyDictionary.

        public readonly InvocationLexer Lexer = new();
        


        /// <summary>
        /// A dictionary of all stored instances.
        /// </summary>
        public readonly IReadOnlyDictionary<string, object> InstanceIndex;
        Dictionary<string, object> _instanceIndex = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// The currently loaded instance from which you can invoke instance and static methods.
        /// </summary>
        ///
        public object? Instance => _instance;
        object? _instance;
        Type? _instanceType;

        /// <summary>
        /// The currently loaded type from which you can invoke methods.
        /// </summary>
        public Type? LoadedType => _loadedType;
        Type? _loadedType;
        Dictionary<string, MethodInfo> _methods = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, FieldInfo> _fields = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A dictionary of all overload methods found in the current loaded type, incase you are having trouble figuring out an overload's changed name. You can also use
        /// the MapOverloads method to see how it is resolved.
        /// </summary>
        public readonly IReadOnlyDictionary<string, MethodInfo> Overloads;
        Dictionary<string, MethodInfo> _overloads = new(StringComparer.OrdinalIgnoreCase);

        // class MemberDictionary
        // {
        //     public readonly Type MembersOf;

        //     public readonly Dictionary<string,MethodInfo> Methods = new(StringComparer.OrdinalIgnoreCase);
        // }

        /// <summary>
        /// This represents the most recently invoked and currently loaded method.
        /// </summary>
        public MethodInfo? LoadedMethod => _loadedMethod;

        MethodInfo? _loadedMethod;

        ParameterInfo[]? _loadedParams;

        static readonly HashSet<string> AmbiguousMatches = new(StringComparer.OrdinalIgnoreCase);

        public const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // HashSet<string>? LoadedTypeAmbiguousMatches = new(StringComparer.OrdinalIgnoreCase);
        public CallInterpreter(IReadOnlyDictionary<string, Type>? book = null)
        {
            LocalCache = book;
            Overloads = new ReadOnlyDictionary<string, MethodInfo>(_overloads);
            InstanceIndex = new ReadOnlyDictionary<string, object>(_instanceIndex);
        }
        public void Clear()
        {
            _instanceIndex.Clear();
            _overloads.Clear();
            _methods.Clear();
            _fields.Clear();
            LocalCache = null;
            _instance = null;
            _loadedType = null;
            _loadedMethod = null;
            _loadedParams = null;

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
            string? sub = input?.Substring(1);
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(sub))
                throw new ArgumentException("Input cannot be null or whitespace.");
            return input[0] switch
            {
                '+' => AddInstance(sub),
                '-' => RemoveInstance(sub),
                '$' => GetInstance(sub),
                '^' => Cast(sub),
                '@' => LoadTypeClearInstance(sub),
                '*' => StaticLoad(sub),
                '#' => InstanceLoad(sub),
                '!' => StaticInvoke(sub),
                _ => Invoke(input, catchinvoke)
            };
        }

        /// <summary>
        /// Convert a method AST object to actual parameters by matching it to a MethodInfo's parameter array.
        /// </summary>
        public object?[] GetParsedParameters(ParameterInfo[] methodparams, Method method)
        {
            if (method.Params.Count != methodparams.Length)
                throw new TargetParameterCountException($"input param count: {method.Params.Count} required count: {methodparams.Length} method name {method.String}");
            object?[] prms = new object[method.Params.Count];
            for (int i = 0; i < methodparams.Length; i++)
                prms[i] = ParameterToObject(method.Params[i], methodparams[i].ParameterType);
            return prms;
        }
        /// <summary>
        /// Invoke from a string after loading a type or instance.
        /// </summary>
        public object? Invoke(string invocation)
        {
            object?[]? objs = LoadInvocation(invocation);
            return LoadedMethod!.Invoke(Instance, objs);
        }

        /// <summary>
        /// Load an instance directly via object reference. Resets loaded method, instance and type.
        /// </summary>
        /// <param name="obj"></param>
        public void DirectLoadInstance(object obj)
        {
            _instance = obj;
            LoadTypePreserveInstance(obj.GetType());
            _instanceType = _loadedType;
        }

        /// <summary>
        /// Load a new type by string. Must be cached in book or typecache. Resets loaded method, instance and type.
        /// </summary>
        /// <param name="typeName"></param>

        public object? LoadTypeClearInstance(string typeName)
        {
            Type t = FindType(TypeString.New(typeName, null));
            LoadTypePreserveInstance(t);
            _instance = null;
            _instanceType = null;
            return null;
        }

        void LoadTypePreserveInstance(Type t)
        {
            _loadedType = t;
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
            _loadedParams = method.GetParameters();
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
            Type t = tname == null ? LoadedType! : FindType(TypeString.New(tname, null)); ;
            object? instance;
            if (t != LoadedType)
            {
                if (t.IsAssignableFrom(_instanceType))
                    LoadTypePreserveInstance(t);
                else
                    throw new InvalidCastException($"{_instanceType} cannot cast to {t}.");
            }
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
            object? instance = StaticInvoke(input) ?? throw new ArgumentException($"Failed to load new instance from {input}");
            DirectLoadInstance(instance);
            return Instance;
        }
        //Isolated lexing, loading and invocation for "quick invocation" without resetting loaded instance, method or type.
        object? StaticInvoke(string input)
        {
            string tname = ResolveMemberAccess(input, out string member, out bool field) ?? throw new ArgumentException($"Cannot static invoke without a type name. Bad input: {input}");
            Type type = FindType(TypeString.New(tname, null));
            object? instance;
            if (!field)
                instance = StaticInvokeMethod(member, type);
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
            return instance;
        }


        //An isolated lexing, loading and invocation for static loading.
        object? StaticInvokeMethod(string input, Type type)
        {
            Method main = Lexer.ParameterTemplate(input);
            var method = MapAndFindMethod(type, main);
            var param = GetParsedParameters(method.GetParameters(), main);
            return method.Invoke(null, param);
        }

        void LoadMethod(Method mthd)
        {
            if (LoadedType == null)
                throw new InvalidOperationException("Must load a type before attempting to invoke.");
            //   if (_localAmbiguousMatches.Contains(mthd.String))
            //   throw new AmbiguousMatchException($"Method named {mthd.String} in type {LoadedType} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
            MethodInfo method = FindMethod(LoadedType, _overloads, _methods, mthd);
            _loadedMethod = method;
            _loadedParams = method.GetParameters();
        }



        void ChangeTMap()
        {
            _loadedParams = null;
            _loadedMethod = null;
            MapMethods(_loadedType!, ref _overloads!, ref _methods!, true);
            MapFields();
        }

        //Begins the cycle - lexes the invocation, loads the method, and then sends data off for parsing.
        object?[]? LoadInvocation(string invocation)
        {
            Method main = Lexer.ParameterTemplate(invocation); //at this point we cannot know if the generic arguments are the same yet, but incase youre reloading the same method with diff generic parameters, we always reload if we detect generic vs generic
            if (LoadedMethod == null || !LoadedMethod.Name.EqualsCaseless(main.String) || LoadedMethod.IsGenericMethod)// && main.IsGeneric)
                LoadMethod(main);
            return GetParsedParameters(_loadedParams!, main);
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
                {       //This should never return null except for Nullable<T> and and classes, it will always throw a Format exception otherwise because the only other things it parses are primitives
                    bool validNullableInput = paramType.IsClass || (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>));
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
                field = FindField(fname, fdeclr);
            else
            {
                field = t.GetField(fname, Flag) ?? throw new MissingFieldException($"No field found in type {t} named {fname}");
                if (!SupportedMember(field))
                    throw new NotSupportedException($"Delegates are unsupported. Bad object: {field.DeclaringType}.{field.Name}");
            }
            return t.IsAssignableFrom(_instanceType) ? field.GetValue(Instance) : field.GetValue(null);
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
                call = MapAndFindMethod(t, method);
            }
            //    if (!Convertible(call.ReturnType, paramType))
            //     throw new ArgumentException($"Method {method.DeclaringType}.{method.String} has a return type of {call.ReturnType}, but the parameter type required for method {LoadedMethod!.Name} is {paramType}");
            ParameterInfo[] methodparams = call.GetParameters();
            object?[]? subparams = GetParsedParameters(methodparams, method);
            object? instance = t.IsAssignableFrom(_instanceType) ? Instance : null;
            return call.Invoke(instance, subparams);
            //if you are getting a type that you know will be in a metadata map, it doesnt need to have case //but if the map is null, then you need full casing  //namespace is ALWAYS required
        }

        MethodInfo MapAndFindMethod(Type t, Method method)
        {
            Dictionary<string, MethodInfo>? overloads = null;
            Dictionary<string, MethodInfo>? methods = null;
            this.MapMethods(t, ref overloads, ref methods);
            return FindMethod(t, overloads!, methods!, method);
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
        object? ParseParameter(string strng, Type type) //need half and int128 support
        {

            //   if (strng.IsWhiteSpace()) //Should give more specific exceptions incase you are using an object parameter (it will ask for method syntax instead of sayingg it cant parse)
            //    goto Throw;
            //  Type mainType = InstanceType ?? LoadedType!;
            if (strng == "this")
            {
                if (Instance == null)
                    throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");
                if (!type.IsAssignableFrom(_instanceType))
                    throw new InvalidCastException($"Current instance is a {_instanceType} and does not cast to parameter type {type}");
                return Instance;
                // if (type.IsAssignableFrom(mainType))
                //     return Instance;
                // else
                //     throw new FormatException($"{InstanceType} does not cast to {type}");
            }
            if (InstanceIndex?.TryGetValue(strng, out object? instance) ?? false)
            {
                if (!type.IsAssignableFrom(instance.GetType()))
                    throw new InvalidCastException($"Instance index object with key {strng} is a {instance.GetType()} and does not cast to parameter type {type}");
                return instance;
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
                object? parsed = null;
                try
                {
                    parsed = Enum.Parse(type, strng, true);
                }
                catch (ArgumentException)
                {

                }
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
#if NET6_0_OR_GREATER
            throw new FormatException($"Tried to parse {strng} to {type}, but value could not parse to {type}.");
#else
            if (type == typeof(nint) || type == typeof(uint)) //i never use nint or uint so i havent fixed this yet but i will later maybe lol
                throw new NotSupportedException("Parsing nint and uint not yet supported for pre-net6.");
            else
                throw new FormatException($"Tried to parse {strng} to {type}, but value could not parse to {type}.");
#endif
        }

        FieldInfo FindField(string fieldName, string fieldDeclaringType)
        {
            _fields.TryGetValue(fieldName, out FieldInfo? field);
            if (field == null)
                throw new MissingFieldException($"No field found named {fieldName} in {fieldDeclaringType}. Delegate type fields are not supported and would have been removed.");
            return field;
        }

        //there is a another suble "parameterless" related bug, harmless, this one with generic parameters
        //if your method is nongeneric, you can input 10,000 generic params, it will parse them, but totally ignore them and invoke your method just fine

        //Finds a method by name in a membermap or overload dictionary, and converts it to a generic method if the AST method object itself has generic parameters.
        MethodInfo FindMethod(Type t, Dictionary<string, MethodInfo> overloads, Dictionary<string, MethodInfo> methods, Method method)
        {
            methods.TryGetValue(method.String, out MethodInfo? realmethod);
            if (realmethod == null)
                overloads.TryGetValue(method.String, out realmethod);
            if (realmethod == null)
                throw new MissingMethodException($"No method named {method.String} found in {t.ToString()}'s method or overload dictionary.");
            if (realmethod.IsGenericMethodDefinition)
                realmethod = method.ConstructGeneric(realmethod, FindType);

            return realmethod;
        }

        Type FindType(TypeString strng)
        {
            Type t = FindType(strng.String);
            if (t.IsGenericTypeDefinition)
                t = strng.ConstructGeneric(t, FindType);
            return t;

        }

        //Finds types in a book or the main cache, converts them to generic based on the generic parameters of the TypeString AST object.
        Type FindType(string strng)
        {
            if (strng == "this")
            {
                if (Instance == null)
                    throw new InvalidOperationException("Cannot invoke from instance as parameter, instance is null.");
                return _instanceType!;
            }
            if (strng == "base")
            {
                if (Instance == null)
                    throw new InvalidOperationException("Cannot invoke from instance as parameter, instance is null.");
                return LoadedType!.BaseType ?? throw new ArgumentException("Base type is null.");
            }
            Type t = TypeCache.GetTypeOrThrow(strng, LocalCache);
            return t;
        }

        void MapMethods(Type type, ref Dictionary<string, MethodInfo>? overloads, ref Dictionary<string, MethodInfo>? methods, bool forceNew = false)
        {
            if (!forceNew && type == LoadedType)//Prevents recreating the same map, but needs to check instance vs static first
            {
                methods = _methods;
                overloads = _overloads;
                return;
            }
            methods ??= new(StringComparer.OrdinalIgnoreCase);
            overloads ??= new(StringComparer.OrdinalIgnoreCase);
            overloads.Clear();
            methods.Clear();
            MapMethods(type, ref methods, ref overloads);

        }

        void MapFields()
        {
            _fields.Clear();
            FieldInfo[] fieldsarray = _loadedType!.GetFields(Flag).Where(SupportedMember).ToArray();
            AddBackwards(_fields, fieldsarray);
        }


        public static void MapMethods(Type t, ref Dictionary<string, MethodInfo> methods, ref Dictionary<string, MethodInfo> overloads)
        {
            MethodInfo[] methodsarray = t.GetMethods(Flag).Where(SupportedMember).ToArray();
            string[] names = methodsarray.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var name in names)
            {
                int count = 0;
                CompareNames(ref count, methodsarray, name, overloads);
            }
            AddBackwards(methods, methodsarray);
        }

        static void AddBackwards<T>(Dictionary<string, T> dic, T[] memberArray) where T : MemberInfo
        {
            for (int i = memberArray.Length - 1; i >= 0; i--)
            {
                T member = memberArray[i];
                dic[member.Name] = member;
                //this is a 'new' resolution thing
            }
        }
        //If you hide an inherited member with 'new', GetField/GetMethod will automatically get the newest member, but GetFields/GetMethods will also return hidden members as well
        //However, the newest member is closest to the 0 index in the info array relative to the older members
        //so functionally the 'older'members are overriden by doing a backwards for loop
        //you can explicitly access those members using casting syntax

        //There is a small issue with "new" resolution - new members are still currently added to the overload dictionary. Currently dont know how I will fix that yet, since it is hard
        //to differentiate overloads and new methods across inheritance hierarchies.

        //AddBackwards also helps cover our overload resolution - the first-declared overload
        static void CompareNames(ref int count, MethodInfo[] methods, string name, Dictionary<string, MethodInfo> overloads)
        {
            foreach (var method in methods)
            {
                if (name.EqualsCaseless(method.Name))
                {
                    count++;
                    if (count > 1)
                        overloads[$"{name}{count - 1}"] = method;

                }
            }
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
            TypeString typeString = TypeString.New(input, null);
            Type t = FindType(typeString);
            if (!t.IsAssignableFrom(_instanceType))
                throw new InvalidCastException($"{_instanceType} cannot cast to {t}.");
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
            if (key == "this" || key == "null" || key == "base")
                throw new ArgumentException($"Key is illegal. Bad Key {key}.");
            if (InvocationLexer.IsDigit(key[0]))
                throw new ArgumentException($"Keys cannot begin with a digit. Bad Key: {key}");
            for (int i = 0; i < key.Length; i++)
                if (InvocationLexer.Illegal(key[i]))
                    throw new ArgumentException($"Keys can only consist of alphanumeric and underscore characters. Bad Key: {key}");
            if (_instanceIndex.TryGetValue(key, out object? val))
            {
                if (!Instance.Equals(val))
                    throw new ArgumentException("Duplicate keyname detected.");
            }
            else
                _instanceIndex[key] = Instance;
            return null;
        }

        object? RemoveInstance(string key)
        {
            _instanceIndex.Remove(key);
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


        string? ResolveMemberAccess(string input, out string member, out bool field) //returns typename, outputs the accessed member
        {
            int? lastAccessorIndex = null;
            int paramStart = input.IndexOf('(');
            field = paramStart == -1;
            //   bool readAccessor = false;
            // bool readingGeneric = false;
            for (int i = 0; i < input.Length; i++) //this resolves typenames vs member names, lexer does something similar but not exactly the same - this one is pretty much universal
            {
                // Lexer.ValidIdentifier(input[i],input,i);
                if (i != paramStart)
                {
                    // if (readAccessor)
                    // {
                    //     Lexer.ValidIdentifierFirstCharOrThrow(input[i], input, i);
                    //     readAccessor = false;
                    // }               //works with anything like field or method() (no type name) or namespace.typename.method(22, "hello", othertype.method()) 
                    if (input[i] == '.')
                    {                      //methods are broken off from the typename with all their parameters included
                        lastAccessorIndex = i;
                        //    readAccessor = true;
                    }
                }
                else
                    break;
                //     }
                //     else if (input[i] == '<')
                //         readingGeneric = true;
                //     else if (input[i] == '>')
                //         readingGeneric = false;
                //     if (readAccessor == false && readingGeneric == false)
                //         Lexer.ValidIdentifier(input[i], input, i);
                // }
                // else
                // break;

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