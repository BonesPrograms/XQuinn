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

        //VARIABLES:
        //You can store the currently loaded instance in a caseless string key dictionary for reloading later on. Begin your string with these Communicators:
        // + adds the loaded instance by key. Example: +MyObject
        // - removes an instance by key. Ex. -MyObject
        // $ loads an instance by key. Ex. $MyObject
        //Keys MUST only consist of alphanumeric characters and/or underscores. The first character of a key CANNOT be a digit.

        //ASSIGNMENT:
        //You can assign to fields using methods, other fields, or literals. Syntax is pretty normal with a few caveats.
        //If instance is loaded, you can access fields on the left hand by name or this.name
        //However, the right hand side of the assingment always needs a type name, even if an instance is loaded
        //Ex.
        //obj = this.Method() as an example of assinging to the instance field named "obj" from an instance method named "Method"

        //KNOWN BUGS

        //1 If you have a method that takes an int? parameter and you return the value using a method that has an int return type, you will get an exception saying
        //the return type doesnt match the parameter type. 


        /// <summary>
        /// Optional, primarily for use with dynamicinvoker so you do not need to use the typecache.
        /// </summary>
        public IReadOnlyDictionary<string, Type>? LocalCache;  ///This is not an actual read only wrapper, it is only a cast so that it can support being passed an IReadOnlyDictionary.
        public readonly InvocationLexer Lexer = new();

        /// <summary>
        /// A dictionary of all stored instances.
        /// </summary>
        public readonly IReadOnlyDictionary<string, object> Variables;
        Dictionary<string, object> _variables = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// The currently loaded instance.
        /// </summary>
        public object? Instance => _instance;
        object? _instance;
        Type? _instanceType; //used for polymorphism checks

        /// <summary>
        /// The currently loaded type.
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

        /// <summary>
        /// This represents the most recently invoked method.
        /// </summary>
        public MethodInfo? LoadedMethod => _loadedMethod;
        MethodInfo? _loadedMethod;
        ParameterInfo[]? _loadedParams;
        static readonly HashSet<string> AmbiguousMatches = new(StringComparer.OrdinalIgnoreCase);
        //this dictionary keeps track of ambiguous matches for method names per type, allows minor efficiency upgrade, we dont always have to map out an entire type if the method is not overloaded
        public const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public CallInterpreter(IReadOnlyDictionary<string, Type>? localCache = null)
        {
            LocalCache = localCache;
            Overloads = new ReadOnlyDictionary<string, MethodInfo>(_overloads);
            Variables = new ReadOnlyDictionary<string, object>(_variables);
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

        /// <summary>
        /// Primary method for interfacing with call interpreter's various features.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="catchinvoke"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public object? Interface(string input, bool catchinvoke = false)
        {
            string? sub = input.Substring(1);
            if (string.IsNullOrWhiteSpace(input) && string.IsNullOrWhiteSpace(sub))
                throw new ArgumentException("Input cannot be null or whitespace.");
            return input[0] switch
            {
                '+' => AddInstanceToVariables(sub), ///Add the current Instance to the Variables dictionary. Returns null.
                '-' => RemoveInstanceFromVariables(sub), ///Remove an instance from the Variables dictionary. Returns null.
                '$' => LoadInstanceFromVariables(sub), ///Load an instance from the Variables dictionary as the current Instance. Returns loaded value.

                '^' => CastInstance(sub), ///Cast the current Instance to a different type. Returns null.
                '@' => LoadTypeStatically(sub), ///Load a type's static members. Returns null.

                '*' => LoadFromStaticMember(sub), ///Load the current Instance from a static field or method. Returns loaded value.
                '#' => LoadFromInstanceMember(sub), ///Load the current Instance from an Instance field or method. Returns loaded value.

                '!' => FastInvoke(sub), ///Invoke a method or field by type name without changing the loaded type. Returns invoked value.
                                        ///Can exclude type name to automatically invoke from the loaded type. This is how you view the values of fields, standard invoke
                                        /// will throw if for anything except method invocations.
                _ => Invoke(input, catchinvoke) ///Invoke a method from the loaded type, or assign. Returns invoked value if invocation. Returns assigned value if assignment.
            };
        }
        /// <summary>
        /// Convert a method AST object to actual parameters by matching it to a MethodInfo's parameter array.
        /// </summary>
        public object?[] GetParsedParameters(ParameterInfo[] methodparams, MethodString method)
        {
            if (method.Params.Count != methodparams.Length)
                throw new TargetParameterCountException($"input param count: {method.Params.Count} required count: {methodparams.Length} method name {method.String}");
            object?[] prms = new object[method.Params.Count];
            for (int i = 0; i < methodparams.Length; i++)
                prms[i] = ParameterToObject(method.Params[i], methodparams[i].ParameterType);
            return prms;
        }
        /// <summary>
        /// Checks if a member is supported by Call Interpreter.
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


        #region Loading

        /// <summary>
        /// Load a new type by string. Must be cached in localcache or typecache. Resets loaded method, instance and type.
        /// </summary>
        /// <param name="typeName"></param>

        public object? LoadTypeStatically(string typeName)
        {
            Type t = FindType(TypeString.New(typeName, null));
            LoadTypeMembers(t);
            _instance = null;
            _instanceType = null;
            return null;
        }

        /// <summary>
        /// Load a method directly. Must be static. Does not reset loaded type or instance.
        /// </summary>
        /// <param name="method"></param>
        /// <exception cref="ArgumentException"></exception>
        public void LoadMethodDirectly(MethodInfo method)
        {
            if (!SupportedMember(method) || !method.IsStatic)
                throw new NotSupportedException($"Method {method} has delegate, in/out/or ref params, or is nonstatic.");
            _loadedMethod = method;
            _loadedParams = method.GetParameters();
        }

        /// <summary>
        /// Load an instance directly via object reference. Resets loaded method, instance and type.
        /// </summary>
        /// <param name="obj"></param>
        void LoadInstance(object obj)
        {
            _instance = obj;
            LoadTypeMembers(obj.GetType());
            _instanceType = LoadedType;
        }
        void LoadTypeMembers(Type t)
        {
            _loadedType = t;
            _loadedParams = null;
            _loadedMethod = null;
            MapMethods(LoadedType!, ref _overloads!, ref _methods!, true);
            MapFields();
        }


        //Begins the cycle - lexes the invocation, loads the method, and then sends data off for parsing.
        object?[]? LoadInvocation(string invocation)
        {
            MethodString main = Lexer.ParameterTemplate(invocation); //at this point we cannot know if the generic arguments are the same yet, but incase youre reloading the same method with diff generic parameters, we always reload if we detect generic vs generic
            if (LoadedMethod == null || !LoadedMethod.Name.EqualsCaseless(main.String) || LoadedMethod.IsGenericMethod)// && main.IsGeneric)
            {
                if (LoadedType == null)
                    throw new InvalidOperationException("Must load a type before attempting to invoke.");
                _loadedMethod = FindMethod(LoadedType, _overloads, _methods, main);
                _loadedParams = _loadedMethod.GetParameters();
            }
            return GetParsedParameters(_loadedParams!, main);
        }
        #endregion
        //If the method input name is equal to the loaded method name, this implies it is a generic method, since we do not allow overloads
        //We always reload on generics, since I cant know at this point what the types of your generic parameters are, they are just strings
        //Technically speaking if you invoke the same generic method with the same type parameters twice, it will recreate it both times, which is less efficient
        //But currently I dont care, I may fix that later

        #region Parsing
        //This sorts between whether or not a parameter is a method invocation as a parameter, or an actual primitive/string value.
        object? ParameterToObject(ParameterString param, Type paramType)
        {
            object? obj;
            if (param is FieldString f)
                obj = GetFieldParameter(FindType(f._type), f.String);
            else if (param is MethodString m)
                obj = InvokeMethodParameter(m, FindType(m._type!));
            else
            {
                obj = ParseParameter(param, paramType);
                if (obj == null)
                {       //This should never return null except for Nullable<T> and and classes, it will always throw a Format exception otherwise because the only other things it parses are primitives
                    bool validNullableInput = paramType.IsClass || (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>));
                    if (!validNullableInput)
                        throw new ArgumentException($"Expected method syntax for type {paramType}, but received {param.String}");
                }       //so if its not throwing format exception, it means it is a class or custom struct parameter (it leaves early and avoids throwing), and it requires method syntax, however it should've already jumped to the Method label early, this means it was parsed incorrectly as a Parameter, and will throw to let you know you messed up
            }
            return obj;

        }

        object? ParseParameter(ParameterString p, Type type) //need half and int128 support
        {
            string strng = p.String;
            if (strng == "this")
            {
                if (Instance == null)
                    throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");
                if (!type.IsAssignableFrom(_instanceType))
                    throw new InvalidCastException($"Current instance is a {_instanceType} and does not cast to parameter type {type}");
                return Instance;
            }
            if (Variables?.TryGetValue(strng, out object? instance) ?? false)
            {
                if (!type.IsAssignableFrom(instance.GetType()))
                    throw new InvalidCastException($"Instance index object with key {strng} is a {instance.GetType()} and does not cast to parameter type {type}");
                return instance;
            }
            return p.ParseParameter(type);
        }


        object? GetFieldParameter(Type t, string fname)
        {
            FieldInfo? field;
            if (t == LoadedType)
            {
                _fields.TryGetValue(fname, out field);
                if (field == null)
                    throw new MissingFieldException($"No field found named {fname} in {LoadedType}. Delegate type fields are not supported and would have been removed.");
            }
            else
            {
                field = t.GetField(fname, Flag) ?? throw new MissingFieldException($"No field found in type {t} named {fname}");
                if (!SupportedMember(field))
                    throw new NotSupportedException($"Delegates are unsupported. Bad object: {field.DeclaringType}.{field.Name}");
            }
            return t.IsAssignableFrom(_instanceType) ? field.GetValue(Instance) : field.GetValue(null);
        }
        object? InvokeMethodParameter(MethodString method, Type t)
        {
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
            if (call == null)
            {
                Dictionary<string, MethodInfo>? overloads = null;
                Dictionary<string, MethodInfo>? methods = null;
                this.MapMethods(t, ref overloads, ref methods);
                call = FindMethod(t, overloads!, methods!, method);
            }
            ParameterInfo[] methodparams = call.GetParameters();
            object?[]? subparams = GetParsedParameters(methodparams, method);
            object? instance = t.IsAssignableFrom(_instanceType) ? Instance : null;
            return call.Invoke(instance, subparams);
            //if you are getting a type that you know will be in a metadata map, it doesnt need to have case //but if the map is null, then you need full casing  //namespace is ALWAYS required
        }

        MethodInfo FindMethod(Type t, Dictionary<string, MethodInfo> overloads, Dictionary<string, MethodInfo> methods, MethodString method)
        {
            methods.TryGetValue(method.String, out MethodInfo? realmethod);
            if (realmethod == null)
                overloads.TryGetValue(method.String, out realmethod);
            if (realmethod == null)
                throw new MissingMethodException($"No method named {method.String} found in {t}'s method or overload dictionary.");
            if (realmethod.IsGenericMethodDefinition)
                realmethod = method.ConstructGeneric(realmethod, FindType);

            return realmethod;
        }

        Type FindType(TypeString strng)
        {
            if (strng.String == "this")
            {
                if (Instance == null)
                    throw new InvalidOperationException("Cannot invoke from instance as parameter, instance is null.");
                return _instanceType!;
            }
            if (strng.String == "base")
            {
                if (Instance == null)
                    throw new InvalidOperationException("Cannot invoke from instance as parameter, instance is null.");
                if (LoadedType == null)
                    throw new ArgumentException("There is no loaded type who's base type can be accessed.");
                return LoadedType.BaseType ?? throw new ArgumentException("Base type is null.");
            }
            Type t = TypeCache.GetTypeOrThrow(strng.String, LocalCache);
            if (t.IsGenericTypeDefinition)
                t = strng.ConstructGeneric(t, FindType);
            return t;

        }

        #endregion

        #region Mapping

        static string? ResolveMemberAccess(string input, out string member, out bool field) //returns typename, outputs the accessed member
        {
            int? lastAccessorIndex = null;
            int paramStart = input.IndexOf('(');
            field = paramStart == -1;
            for (int i = 0; i < input.Length; i++) //this resolves typenames vs member names, lexer does something similar but not exactly the same - this one is pretty much universal
            {                                        //works with anything like field or method() (no type name) or namespace.typename.method(22, "hello", othertype.method()) //methods are broken off from the typename with all their parameters included
                if (i == paramStart)
                    break;
                if (input[i] == '.')
                    lastAccessorIndex = i;
            }
            if (lastAccessorIndex != null)
            {
                member = input.Substring(lastAccessorIndex.Value + 1);
                string typename = input.Remove(lastAccessorIndex.Value);
                return typename;
            }
            member = input;
            return null; //no type name, just a member, this technically isnt allowed but i let you get away with it for instance loading 
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
            FieldInfo[] fieldsarray = LoadedType!.GetFields(Flag).Where(SupportedMember).ToArray();
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

        #endregion

        #region Interfacing

        ///Checks for method or field syntax. If it detects a method, it runs the entire CallInterpreter like normal and assigns the returned value.
        object? LoadFromInstanceMember(string input)
        {
            if (Instance == null)
                throw new InvalidOperationException("Cannot load from instance, instance is null.");
            string? tname = ResolveMemberAccess(input, out string member, out bool field);
            Type t = tname == null ? LoadedType! : FindType(TypeString.New(tname, null)); ;
            object? instance;
            if (t != LoadedType)
            {
                if (t.IsAssignableFrom(_instanceType))
                    LoadTypeMembers(t);
                else
                    throw new InvalidCastException($"{_instanceType} cannot cast to {t}.");
            }
            if (!field)
                instance = Invoke(member);
            else
                instance = GetFieldParameter(t, member);
            if (instance == null)
                throw new ArgumentException($"Load returned null, unable to load instance. Input: {input}");
            LoadInstance(instance);
            return Instance;
        }


        //Checks for method or field syntax. If it detects a method, it diverts to an isolated type load and method invocation.
        object? LoadFromStaticMember(string input)
        {
            object instance = FastInvoke(input) ?? throw new ArgumentException($"Failed to load new instance from {input}");
            LoadInstance(instance);
            return Instance;
        }
        //Isolated lexing, loading and invocation for "quick invocation" without resetting loaded instance, method or type.
        object? FastInvoke(string input)
        {
            string? tname = ResolveMemberAccess(input, out string member, out bool field);
            Type type;
            if (tname == null)
                type = LoadedType ?? throw new ArgumentException("Cannot FastInvoke from loaded type, loaded type is null.");
            else
                type = FindType(TypeString.New(tname, null));
            object? instance;
            if (!field)
                instance = InvokeMethodParameter(Lexer.ParameterTemplate(input), type);
            else
                instance = GetFieldParameter(type, member);
            return instance;
        }

        /// <summary>
        /// Invoke from a string after loading a type or instance.
        /// </summary>
        object? Invoke(string invocation)
        {
            object?[]? objs = LoadInvocation(invocation);
            return LoadedMethod!.Invoke(Instance, objs);
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

        // object? GetFieldValue(string input)
        // {
        //     string? tname = ResolveMemberAccess(input, out string member, out bool field);
        //     if (!field)
        //         throw new ArgumentException("Input must be a field.");
        //     Type t = tname == null ? LoadedType! : FindType(TypeString.New(tname, null));
        //     return GetFieldParameter(t, member);
        // }
        object? CastInstance(string input)
        {
            if (Instance == null)
                throw new InvalidOperationException("Cannot cast, instance is null.");
            TypeString typeString = TypeString.New(input, null);
            Type t = FindType(typeString);
            if (!t.IsAssignableFrom(_instanceType))
                throw new InvalidCastException($"{_instanceType} cannot cast to {t}.");
            LoadTypeMembers(t);
            return null;
        }

        object? Invoke(string input, bool catchinvoke)
        {
            string[] assignmnet = input.Split('=');
            if (assignmnet.Length > 1)
                return Assignment(assignmnet, input);
            if (!catchinvoke)
                return Invoke(input);
            CatchInvoke(input);
            return null;
        }

        object? Assignment(string[] assignment, string originalinput)
        {
            if (assignment.Length != 2)
                throw new ArgumentException($"Invalid assignment, can only contain left hand and right hand. Bad assignment: {originalinput}");
            string lefthand = assignment[0];
            string righthand = assignment[1];

            string? lefthandTypeName = ResolveMemberAccess(lefthand, out lefthand, out bool lefthandfield);
            if (!lefthandfield)
                throw new ArgumentException($"Can only assign to fields. Bad input: {lefthand}");
            Type lefthandtype;
            if (lefthandTypeName == null)
                lefthandtype = LoadedType ?? throw new ArgumentException($"There is no loaded type to assign fields to. Bad input: {originalinput}");
            else
                lefthandtype = FindType(TypeString.New(lefthandTypeName, null));
            FieldInfo f = lefthandtype.GetField(lefthand.Trim(), Flag) ?? throw new MissingFieldException($"No field found in type {lefthandtype} named {lefthand}");

            string? righthandTypeName = ResolveMemberAccess(righthand, out righthand, out bool righthandfield);
            object? obj;
            if (righthandTypeName != null)
            {
                Type righthandType = FindType(TypeString.New(righthandTypeName, null));

                if (righthandfield)
                    obj = GetFieldParameter(righthandType, righthand.Trim());
                else
                {
                    MethodString main = Lexer.ParameterTemplate(righthand);
                    obj = InvokeMethodParameter(main, righthandType);
                }
            }
            else
                obj = ParseParameter(new(righthand.Trim()), f.FieldType);

            if (lefthandtype.IsAssignableFrom(_instanceType))
                f.SetValue(Instance, obj);
            else
                f.SetValue(null, obj);
            return obj;
        }

        object? RemoveInstanceFromVariables(string key)
        {
            _variables.Remove(key);
            return null;
        }

        object? AddInstanceToVariables(string key)
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
            if (_variables.TryGetValue(key, out object? val))
            {
                if (!Instance.Equals(val))
                    throw new ArgumentException("Duplicate keyname detected.");
            }
            else
                _variables[key] = Instance;
            return null;
        }


        object LoadInstanceFromVariables(string key)
        {
            if (_variables == null)
                throw new InvalidOperationException("No instances are currently stored.");
            if (!_variables.TryGetValue(key, out object? instance))
                throw new ArgumentException($"There is no stored instance with the key {key}.");
            LoadInstance(instance);
            return instance;
        }
        #endregion



    }

}