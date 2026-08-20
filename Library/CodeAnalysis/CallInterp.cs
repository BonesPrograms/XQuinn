using System.Reflection;
using XQuinn.Extensions;
using System.Text.RegularExpressions;
using XQuinn.Reflection;
using XQuinn.Parsing;
using XQuinn.CodeAnalysis.AST;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using XQuinn.CodeAnalysis;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using XQuinn.NetConsole;

namespace XQuinn.CodeAnalysis
{

    public sealed class Variable
    {
        public readonly string VariableKey;
        public readonly string TypeCacheKey;
        public readonly object Object;
        public readonly Type ObjectType;
        public Variable(string typekey, string varkey, object instance)
        {
            VariableKey = varkey;
            TypeCacheKey = typekey;
            Object = instance;
            ObjectType = instance.GetType();
        }

        public override string ToString()
        {
            return $"VariableKey: {VariableKey} ObjectType: {ObjectType} TypeKey: {TypeCacheKey} Object: {Object}";
        }
    }

    public readonly struct Overload : IEquatable<Overload>
    {
        public readonly int Index;
        public readonly string Key; //the exact key input by the user - usually includes an index like method2()
        public ReadOnlySpan<char> KeySpan => _memory.Span; //This slices off the index from the end of the string, so that we can use it later
        readonly ReadOnlyMemory<char> _memory; //to compare to actual method names. Ex. key "method1" would fail to find an overload if its actual name was "method"
        public Overload(string name, int index)
        {
            Key = name;
            Index = index;
            int last = name.Length - 1;
            int range = InvocationLexer.IsDigit(name[last]) ? last : last + 1;
            _memory = Key.AsMemory(0, range);
        }

        bool IEquatable<Overload>.Equals(Overload obj)
        {
            return Equals(obj);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            foreach (char c in KeySpan) hash.Add(Normalize(c));// hash.Add(char.ToUpperInvariant(c));
            hash.Add(Index);
            return hash.ToHashCode();
            // return HashCode.Combine(hash.ToHashCode(), Index);
        }

        static char Normalize(char c) =>
            c is >= 'a' and <= 'z'
                ? (char)(c - ('a' - 'A'))
                : c;
        public override string ToString() => Key;
        public bool Equals(Overload overload)
        {
            return overload.Index == Index && overload.KeySpan.Equals(KeySpan, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is Overload overload && Equals(overload);
        } //obj is Overload overload && ((IEquatable<Overload>)this).Equals(overload);

        public static bool operator ==(Overload left, Overload right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Overload left, Overload right)
        {
            return !(left == right);
        }
    }

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
        // * Load from an instance or type member.
        // @ Load a type's static members

        //Method Invocation Communicators:
        // ( Parameters Begin
        // ) Parameters End
        // , Parameter End
        // . Member Access (only goes one layer. IE. TypeName.FieldName, not TypeName.FieldName.MethodName)
        // " String Declaration (opening and closing)
        // ' Char Declaration (opening and closing) digits and letters technically don't need char declaration enclosures, but i recommend to use them habitually
        // \ Escape Sequence (for strings, chars dont need escapes in this interpreter)

        //LOADING:

        //Load a type's static members
        //@TypeName

        //Load an Instance from a Field or Method:
        //*TypeName.Field or *TypeName.Method() for static or base access if an instance is loaded
        //*this.Field() or *this.Method() for instance access
        //You do not need to use "this" or "typename" if you are accessing a member from the loaded type.

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

        //Fields and Methods passed as parameters must always have a typename or "this"

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
        //ex. *BaseType.Field
        //For parameters, instead of accessing by this, you would access it by type name, again, similar to static syntax
        //ex. BaseType.Method()

        //This feature also works for accessing base members that have been hidden by the 'new' keyword.
        //This also works the other way around, so when you cast to a base type, you can explicitly access members of the instance's actual type by leading with the instance's type name.

        //You can also optionally use the 'base' keyword, this will get the instance's basetype. ex.
        // base.Method()

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
        //If a type is loaded, you can access fields on the left hand by name
        //However, the right hand side of the assingment always needs a type name or this
        //Ex.
        //obj = this.Method() as an example of assinging to the instance field named "obj" from an instance method named "Method"
        //You can assign to any static members outside of the loaded type by leading with a typename on the lefthand
        //You can also assign to variables in the variable dictionary by leading with it's key on the lefthand

        //FASTINVOKE/GET FIELDS
        //To see a field value, begin your invocation with !
        //IE. !FieldName for a field from the loaded type. You can insert typename to get any static field.
        //You can also use ! to invoke a static member directly from a type, or to cast the instance and invoke from a base method/field in one quick invocation.
        //without actually changing the loaded type itself.

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
       // public readonly IReadOnlyDictionary<string, Variable> _variables;
        internal readonly Dictionary<string, Variable> _variables = new(StringComparer.OrdinalIgnoreCase);

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
        internal Dictionary<string, MethodInfo> _methods = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<Overload, MethodInfo> _overloads = new();
        internal readonly Dictionary<string, FieldInfo> _fields = new(StringComparer.OrdinalIgnoreCase);



        /// <summary>
        /// A dictionary of all overload methods found in the current loaded type, incase you are having trouble figuring out an overload's changed name. You can also use
        /// the MapOverloads method to see how it is resolved.
        /// </summary>
        //    public readonly IReadOnlyDictionary<string, MethodInfo> Overloads;


        /// <summary>
        /// This represents the most recently invoked method.
        /// </summary>
        public MethodInfo? LoadedMethod => _loadedMethod;
        MethodInfo? _loadedMethod;
        ParameterInfo[]? _loadedParams;
        static readonly Dictionary<Type, HashSet<string>> AmbiguousMatches = new();
        static readonly Dictionary<Type, Dictionary<string, MemberInfo>> MemberCache = new(); //all members ever accessed by callinterp 
        static readonly Dictionary<string, Type> GenericsCache = new(StringComparer.OrdinalIgnoreCase); //reified generics

        /// <summary>
        /// Caching adds methods and fields to a global cache as they are invoked by CallInterpreter.
        /// </summary>
        public bool Caching = true;
        //this dictionary keeps track of ambiguous matches for method names per type, allows minor efficiency upgrade, we dont always have to map out an entire type if the method is not overloaded
        public const BindingFlags Flag = BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static void FlushStaticCache(bool ambiguousMatches = false, bool typeMembers = true, bool reifiedGenerics = true)
        {
            if (ambiguousMatches)
                AmbiguousMatches.Clear(); //technically not part of 'caching' system, doesnt care if caching is false, will always store ambiguous matches
            if (typeMembers)                    //may be a thing later, for now you can clear at least
                MemberCache.Clear();
            if (reifiedGenerics)
                GenericsCache.Clear();
        }

        public void RunCommands(params string[] commands)
        {
            RunCommands((IEnumerable<string>)commands);
        }
        public void RunCommands(IEnumerable<string> commands)
        {
            foreach (string command in commands) Interface(command);
        }
        public CallInterpreter(IReadOnlyDictionary<string, Type>? localCache = null)
        {
            LocalCache = localCache;
            //   Overloads = new ReadOnlyDictionary<string, MethodInfo>(_overloads);
            //Variables = new ReadOnlyDictionary<string, Variable>(_variables);
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
            if (string.IsNullOrWhiteSpace(invocation) || invocation.Length < 2)
                throw new ArgumentException("Input cannot be null, whitespace, or less than 2 chars in length.");
            return invocation[0] switch
            {
                '+' => AddViarable(invocation.Substring(1)), ///Add the current Instance to the Variables dictionary. Returns null.
                '-' => RemoveVariable(invocation.Substring(1)), ///Remove an instance from the Variables dictionary. Returns null.
                '$' => LoadVariable(invocation.Substring(1)), ///Load an instance from the Variables dictionary as the current Instance. Returns loaded value.

                '@' => LoadType(invocation.Substring(1)), ///Load a type's static members. Returns null.
                '*' => LoadInstance(invocation.Substring(1)), ///Load the current Instance from a field or method. Returns loaded value.
                '^' => CastInstance(invocation.Substring(1)), ///Cast the current Instance to a different type. Returns null.

                '!' => IsolatedInvoke(invocation.Substring(1)), ///Invoke a method or field by type name without changing the loaded type. Returns invoked value.
                                                                ///Can exclude type name to automatically invoke from the loaded type. This is how you view the values of fields, standard invoke
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

        public object? LoadType(string typeName)
        {
            TypeString tstring = TypeString.New(typeName, null);
            Type t = FindType(tstring);
            _key = tstring.String;
            LoadTypeMembers(t);
            _instance = null;
            _instanceType = null;
            _variableKey = null;
            return null;
        }

        /// <summary>
        /// Load a method directly. Must be static. Does not reset loaded type or instance.
        /// </summary>
        /// <param name="method"></param>
        /// <exception cref="ArgumentException"></exception>
        public void LoadMethodDirectly(MethodInfo method)
        {
            if (!SupportedMember(method) || !method.IsStatic) throw new NotSupportedException($"Method {method} has delegate, in/out/or ref params, or is nonstatic, or has ref return type.");
            _loadedMethod = method;
            _loadedParams = method.GetParameters();
        }


        void LoadInstance(object instance, Type instanceType)
        {
            _instance = instance;
            LoadTypeMembers(instanceType);
            _instanceType = instanceType;
        }
        void LoadTypeMembers(Type type)
        {
            _loadedType = type;
            _loadedParams = null;
            _loadedMethod = null;
            MapMethods(LoadedType!, ref _overloads!, ref _methods!, null, true);
            MapFields();
        }


        //Begins the cycle - lexes the invocation, loads the method, and then sends data off for parsing.
        object?[]? LoadInvocation(string invocation)
        {
            MethodString main = Lexer.ParameterTemplate(invocation, null); //at this point we cannot know if the generic arguments are the same yet, but incase youre reloading the same method with diff generic parameters, we always reload if we detect generic vs generic
            if (LoadedMethod == null || !LoadedMethod.Name.EqualsCaseless(main.String) || LoadedMethod.IsGenericMethod)// && main.IsGeneric)
            {
                if (LoadedType == null) throw new InvalidOperationException("Must load a type before attempting to invoke.");
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

        /// <summary>
        /// Convert a method AST object to actual parameters by matching it to a MethodInfo's parameter array.
        /// </summary>
        public object?[] GetParsedParameters(ParameterInfo[] actualParameters, MethodString invocation)
        {
            object?[] prms = new object[actualParameters.Length];
            int inputAmount = invocation.Params.Count;
            int reqAmount = actualParameters.Length;
            int lastparam = reqAmount - 1;
            if (invocation.Params.Count != reqAmount) ResolveUnequalParameters(inputAmount, reqAmount, actualParameters, prms, invocation, lastparam);
            else if (lastparam >= 0 && actualParameters[lastparam].IsDefined(typeof(ParamArrayAttribute))) GetParamArray(lastparam, actualParameters, prms, invocation);
            else for (int i = 0; i < actualParameters.Length; i++) prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
            return prms;
        }

        void ResolveUnequalParameters(int inputAmount, int reqAmount, ParameterInfo[] actualParameters, object?[] prms, MethodString invocation, int lastparam)
        {
            if (inputAmount < reqAmount)
            {
                for (int i = inputAmount; i < reqAmount; i++)
                {
                    ParameterInfo parameter = actualParameters[i];
                    if (parameter.HasDefaultValue) prms[i] = parameter.DefaultValue;
                    else throw new TargetParameterCountException($"Parameter {parameter} does not have a default value. Input param count {inputAmount} Required count {reqAmount}");
                }
                for (int i = 0; i < inputAmount; i++) prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
            }
            else if (lastparam >= 0 && actualParameters[lastparam].IsDefined(typeof(ParamArrayAttribute))) GetParamArray(lastparam, actualParameters, prms, invocation);
            else throw new TargetParameterCountException($"input param count: {invocation.Params.Count} required count: {actualParameters.Length} method name {invocation.String}");
        }

        void GetParamArray(int lastparam, ParameterInfo[] actualParameters, object?[] prms, MethodString invocation)
        {
            int lastBeforeThat = lastparam - 1;
            if (lastBeforeThat >= 0) for (int i = 0; i < lastBeforeThat; i++) prms[i] = ParameterToObject(invocation.Params[i], actualParameters[i].ParameterType);
            Type elementType = actualParameters[lastparam].ParameterType.GetElementType() ?? throw new ArgumentException();
            Array paramArray = Array.CreateInstance(elementType, invocation.Params.Count - lastparam); //assign array type via reflection becuase object[] wont work for params keyword
            for (int i = lastparam; i < invocation.Params.Count; i++) paramArray.SetValue(ParameterToObject(invocation.Params[i], elementType), i - lastparam);
            prms[lastparam] = paramArray;
        }
        //This sorts between whether or not a parameter is a method invocation as a parameter, or an actual primitive/string value.
        object? ParameterToObject(ParameterString value, Type paramType)
        {
            object? obj;
            if (value is FieldString field) obj = GetFieldWithVariable(field);
            else if (value is MethodString method) obj = InvokeMethodWithVariable(method);
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
            if (strng == "this")
            {
                if (LoadedInstance == null) throw new InvalidOperationException("Cannot pass this as parameter, instance is null.");
                if (!paramType.IsAssignableFrom(_instanceType)) throw new InvalidCastException($"Current instance is a {_instanceType} and does not cast to parameter type {paramType}");
                return LoadedInstance;
            }
            if (_variables?.TryGetValue(strng, out Variable? variable) ?? false)
            {
                if (!paramType.IsAssignableFrom(variable.ObjectType)) throw new InvalidCastException($"Instance index object with key {strng} is a {variable.ObjectType} and does not cast to parameter type {paramType}");
                else return variable.Object;
            }
            return value.ParseValue(paramType);
        }



        object? GetFieldParameter(Type fromType, FieldString fieldstring, Variable? localvar)
        {
            object? variable = localvar?.Object;
            string fname = fieldstring.String;
            FieldInfo? field = CheckGlobalCache<FieldInfo>(fieldstring.String, fromType, out bool typeCached, out bool fieldCached);
            if (field == null && fromType == LoadedType)
            {
                _fields.TryGetValue(fname, out field);
                if (field == null) throw new MissingFieldException($"No field found named {fname} in {LoadedType}. Delegate type fields are not supported and would have been removed.");
            }
            else if (field == null)
            {
                field = fromType.GetField(fname, Flag) ?? throw new MissingFieldException($"No field found in type {fromType} named {fname}");
                if (!SupportedMember(field)) throw new NotSupportedException($"Delegates are unsupported. Bad object: {field.DeclaringType}.{field.Name}");
            }
            CacheMember(typeCached, fieldCached, fromType, field, fieldstring.String);
            return field.GetValue(GetVariableInstance(fromType, variable));
        }
        object? InvokeMethodParameter(MethodString method, Type fromType, Variable? localvar)
        {
            object? variable = localvar?.Object;
            MethodInfo? call = CheckGlobalCache<MethodInfo>(method.NameWithGenerics, fromType, out bool typeCached, out bool methodCached);
            if (call == null && fromType != LoadedType)
            {
                AmbiguousMatches.TryGetValue(fromType, out HashSet<string>? methods);
                bool val = !methods?.Contains(method.String) ?? true;
                if (val)
                {
                    try { call = fromType.GetMethod(method.String, Flag); }
                    catch (AmbiguousMatchException)
                    {
                        if (methods == null) { methods = new(StringComparer.OrdinalIgnoreCase); AmbiguousMatches[fromType] = methods; }
                        methods.Add(method.String);
                        throw new AmbiguousMatchException($"Method named {method.String} in type {fromType} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
                    }
                }
                if (!val || call == null)
                {
                    Dictionary<Overload, MethodInfo>? overloads = null;
                    Dictionary<string, MethodInfo>? _ = null;
                    char last = method.String[method.String.Length - 1];
                    Overload overload = InvocationLexer.IsDigit(last) ? new(method.String, last - '0') : new(method.String, 0);
                    this.MapMethods(fromType, ref overloads, ref _, overload, true);
                    call = FindMethod(fromType, overloads!, null, method) ?? throw new ArgumentNullException();
                }


                // else
                //   throw new AmbiguousMatchException($"Method named {method.String} in type {t} has multiple overloads and it's name has been modified (see CallInterp Overloads for details.)");
            }
            if (call == null && fromType == LoadedType)
            {
                //Dictionary<Overload, MethodInfo>? overloads = null;
                //Dictionary<string, MethodInfo>? methods = null;
                // this.MapMethods(fromType, ref overloads, ref methods);
                call = FindMethod(fromType, _overloads, _methods, method);
            }
            else if (call?.IsGenericMethodDefinition ?? throw new ArgumentNullException()) call = method.ConvertToGeneric(call, LocalCache);
            if (!methodCached && !SupportedMember(call)) throw new ArgumentException($"Method {call} in type {call.DeclaringType} has invalid in out or ref params or ref returntype");
            CacheMember(typeCached, methodCached, fromType, call, method.NameWithGenerics);
            return call.Invoke(GetVariableInstance(fromType, variable), GetParsedParameters(call.GetParameters(), method));
            //if you are getting a type that you know will be in a metadata map, it doesnt need to have case //but if the map is null, then you need full casing  //namespace is ALWAYS required
        }

        object? GetVariableInstance(Type paramType, object? variable)
        {
            return variable ?? (paramType.IsAssignableFrom(_instanceType) ? LoadedInstance : null);
        }
        object? InvokeMethodWithVariable(MethodString method)
        {
            Type t = FindTypeWithVariable(method, out Variable? variable);
            return InvokeMethodParameter(method, t, variable);
        }
        object? GetFieldWithVariable(FieldString field)
        {
            Type t = FindTypeWithVariable(field, out Variable? variable);
            return GetFieldParameter(t, field, variable);
        }
        #endregion

        #region Finding

        /// <summary>
        /// Checks if a member is supported by Call Interpreter.
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        public static bool SupportedMember(MemberInfo member)
        {
            if (member is MethodInfo method) return method.ReturnType.IsByRef || !method.GetParameters().Any(x => x.IsOut || x.IsIn || x.ParameterType.IsByRef || typeof(Delegate).IsAssignableFrom(x.ParameterType));
            else if (member is FieldInfo field) return !typeof(Delegate).IsAssignableFrom(field.FieldType);
            else return false;
        }

        Type FindTypeWithVariable(IMemberString member, out Variable? variable)
        {
            if (member.DeclaringType == null) throw new ArgumentException();
            if (_variables.TryGetValue(member.DeclaringType.String, out variable)) return variable.ObjectType; else return FindType(member.DeclaringType);
        }

        Type FindType(TypeString typename, bool explicitCast = false)
        {
            if (!explicitCast)
            {
                if (_variables.TryGetValue(typename.String, out Variable? variable)) return variable.ObjectType;
                if (typename.String == "this") return _instanceType ?? throw new InvalidOperationException("Cannot pass this, instance is null.");
                if (typename.String == _key) return LoadedType ?? throw new InvalidOperationException();

            }
            if (typename.String == "base") { if (_instanceType == null) throw new InvalidOperationException("Cannot get instance base, instance is null."); else return _instanceType.BaseType ?? throw new ArgumentException("Base type of instance is null."); }
            Type t = TypeCache.GetTypeOrThrow(typename.String, LocalCache);
            if (t.IsGenericTypeDefinition)
            {
                if (GenericsCache.TryGetValue(typename.NameWithGenerics, out Type? generic)) return generic;
                t = typename.ConvertToGeneric(t, LocalCache);
                if (Caching) GenericsCache[typename.NameWithGenerics] = t;
            }
            return t;

        }

        MethodInfo FindMethod(Type fromType, Dictionary<Overload, MethodInfo> overloads, Dictionary<string, MethodInfo>? methods, MethodString method, Overload? overloadQuery = null)
        {
            MethodInfo? realmethod = null;
            methods?.TryGetValue(method.String, out realmethod);
            if (realmethod == null)
            {
                char? last = overloadQuery == null ? method.String[method.String.Length - 1] : null;
                Overload overload = last == null ? overloadQuery!.Value : InvocationLexer.IsDigit(last.Value) ? new(method.String, last.Value - '0') : new(method.String, 0);
                overloads.TryGetValue(overload, out realmethod);
            }
            if (realmethod == null) throw new MissingMethodException($"No method named {method.String} found in {fromType}'s method or overload dictionary.");
            if (realmethod.IsGenericMethodDefinition) realmethod = method.ConvertToGeneric(realmethod, LocalCache);
            return realmethod;
        }

        T? CheckGlobalCache<T>(string key, Type fromType, out bool typeCached, out bool memberCached) where T : MemberInfo
        {
            memberCached = false;
            typeCached = false;
            if (fromType == LoadedType) return null;
            typeCached = MemberCache.TryGetValue(fromType, out var cachedMembers);
            if (typeCached)
            {
                memberCached = cachedMembers!.TryGetValue(key, out MemberInfo? member);
                if (memberCached) return (T)member!;
            }
            return null;
        }
        void CacheMember(bool typeCached, bool memberCached, Type fromType, MemberInfo member, string key)
        {
            if (Caching && fromType != LoadedType)
            {
                Dictionary<string, MemberInfo> cachedMembers;
                if (!typeCached) { cachedMembers = new(StringComparer.OrdinalIgnoreCase); MemberCache[fromType] = cachedMembers; }
                else cachedMembers = MemberCache[fromType];
                if (!memberCached) cachedMembers[key] = member;
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


        void MapMethods(Type fromType, ref Dictionary<Overload, MethodInfo>? overloads, ref Dictionary<string, MethodInfo>? methods, Overload? overloadSingleSearch = null, bool forceNew = false)
        {
            if (!forceNew && fromType == LoadedType) { methods = _methods; overloads = _overloads; return; }
            if (overloadSingleSearch == null && methods == null) methods = new(StringComparer.OrdinalIgnoreCase); else methods?.Clear();
            if (overloads == null) overloads = new(); else overloads.Clear();
            MapMethods(fromType, ref methods, ref overloads, overloadSingleSearch);

        }

        void MapFields()
        {
            _fields.Clear();
            AddBackwards(_fields, LoadedType!.GetFields(Flag).Where(SupportedMember).ToArray());
        }


        public static void MapMethods(Type fromType, ref Dictionary<string, MethodInfo>? methods, ref Dictionary<Overload, MethodInfo> overloads, Overload? overloadSingleSearch)
        {
            MethodInfo[] methodsarray = fromType.GetMethods(Flag).Where(SupportedMember).ToArray();
            if (overloadSingleSearch == null)
            {
                IEnumerable<string> names = methodsarray.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase);
                if (methods != null) AddBackwards(methods, methodsarray);
                foreach (string name in names) CompareNames(methodsarray, name, methods, overloads);
            }
            else CompareNames(methodsarray, overloadSingleSearch.Value.KeySpan, methods, overloads, overloadSingleSearch.Value.Index);


        }

        static void AddBackwards<T>(Dictionary<string, T> storage, T[] memberArray) where T : MemberInfo
        {
            for (int i = memberArray.Length - 1; i >= 0; i--) { T member = memberArray[i]; storage[member.Name] = member; }
        }
        //this is a 'new' resolution thing
        //If you hide an inherited member with 'new', GetField/GetMethod will automatically get the newest member, but GetFields/GetMethods will also return hidden members as well
        //However, the newest member is closest to the 0 index in the info array relative to the older members
        //so functionally the 'older'members are overriden by doing a backwards for loop
        //you can explicitly access those members using casting syntax

        //There is a small issue with "new" resolution - new members are still currently added to the overload dictionary. Currently dont know how I will fix that yet, since it is hard
        //to differentiate overloads and new methods across inheritance hierarchies.

        //AddBackwards also helps cover our overload resolution - the first-declared overload
        static void CompareNames(MethodInfo[] methods, ReadOnlySpan<char> name, Dictionary<string, MethodInfo>? methoddic, Dictionary<Overload, MethodInfo> overloads, int? limit = null)
        {
            int count = 0;
            limit++;
            MethodInfo? first = null;
            foreach (MethodInfo method in methods)
            {
                if (name.Equals(method.Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                    if (count == 1) first = method;
                    else
                    {
                        overloads[new(method.Name, count - 1)] = method;
                        if (first != null)
                        {
                            overloads[new(first.Name, 0)] = first;
                            //, methoddic.Remove(first.Name);
                            first = null;
                        }
                    }
                    if (count >= limit)
                    {
                        if (first != null)
                        {
                            overloads[new(first.Name, 0)] = first;
                            //, methoddic.Remove(first.Name);
                        }
                        return;
                    }
                }

            }
            // foreach (MethodInfo method in methods) if (name.EqualsCaseless(method.Name)) { count++; if (count > 1) overloads[$"{name}{count - 1}"] = method; }
        }

        #endregion

        #region Interfacing


        //Checks for method or field syntax. If it detects a method, it diverts to an isolated type load and method invocation.
        object LoadInstance(string invocation)
        {
            object instance = IsolatedInvoke(invocation) ?? throw new ArgumentException($"Failed to load new instance from {invocation}");
            LoadInstance(instance, instance.GetType());
            _variableKey = null;
            _key = "this";
            return instance;
        }
        //Isolated lexing, loading and invocation for "quick invocation" without resetting loaded instance, method or type.
        object? IsolatedInvoke(string invocation)
        {
            string typeName = ResolveMemberAccess(invocation, out string member, out bool field) ?? _key ?? throw new InvalidOperationException();
            if (!field) return InvokeMethodWithVariable(Lexer.ParameterTemplate(member, typeName!));
            else return GetFieldWithVariable(new(member, null, TypeString.New(typeName!, null)));
        }


        object? CastInstance(string invocation)
        {
            if (LoadedInstance == null) throw new InvalidOperationException("Cannot cast, instance is null.");
            TypeString tstring = TypeString.New(invocation, null);
            Type t = FindType(tstring, true);
            _key = tstring.String;
            if (!t.IsAssignableFrom(_instanceType)) throw new InvalidCastException($"{_instanceType} cannot cast to {t}.");
            LoadTypeMembers(t);
            return null;
        }

        object? StandardInvokeOrAssign(string invocation)
        {
            if (Assignment(invocation, out object? assigned)) return assigned;
            object? returned;
            object?[]? parameters = LoadInvocation(invocation);
            try { returned = LoadedMethod!.Invoke(LoadedInstance, parameters); }
            catch (TargetInvocationException ex) { throw new CapturedException(LoadedMethod!, ex.InnerException); }
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
                lefthandtype = FindTypeWithVariable(new FieldString(lefthand, null, TypeString.New(lefthandTypeName, null)), out Variable? variable);
                instance = variable?.Object;
            }

            FieldInfo assigningTo = lefthandtype.GetField(lefthand, Flag) ?? throw new MissingFieldException($"No field found in type {lefthandtype} named {lefthand}");
            string? righthandTypeName = ResolveMemberAccess(righthand, out righthand, out bool righthandfield);
            if (righthandTypeName != null)
            {
                if (righthandfield) assignedValue = GetFieldWithVariable(new(righthand.Trim(), null, TypeString.New(righthandTypeName, null)));
                else assignedValue = InvokeMethodWithVariable(Lexer.ParameterTemplate(righthand, righthandTypeName));

            }
            else
                assignedValue = ParseParameter(new(righthand.Trim(), null), assigningTo.FieldType);
            //   instance = GetVariableInstance(lefthandtype, instance);
            assigningTo.SetValue(instance, assignedValue);
            return true;
        }

        object? RemoveVariable(string key)
        {
            _variables.Remove(key);
            if (key.EqualsCaseless(_variableKey)) _variableKey = null;
            return null;
        }

        object? AddViarable(string key)
        {
            if (LoadedInstance == null) throw new InvalidOperationException("No instance is loaded.");
            TypeCache.ThrowIfBadKey(key);
            Type? typeWithConflictingKey = TypeCache.GetTypeCached(key, LocalCache);
            if (typeWithConflictingKey != null) throw new ArgumentException($"Key {key} is already taken by a cached type, and cannot be used as a name for a local variable. Names are not case sensitive.");
            if (_variables.TryGetValue(key, out Variable? variable)) { if (!ReferenceEquals(LoadedInstance, variable.Object)) throw new ArgumentException("Duplicate keyname detected."); }
            else _variables[key] = new(_key ?? "this", key, LoadedInstance);
            _variableKey = key;
            return null;
        }


        Variable LoadVariable(string key)
        {
            if (_variables == null) throw new InvalidOperationException("No instances are currently stored.");
            if (!_variables.TryGetValue(key, out Variable? variable)) throw new ArgumentException($"There is no stored instance with the key {key}.");
            _variableKey = key;
            _key = variable.TypeCacheKey;
            LoadInstance(variable.Object, variable.ObjectType);
            return variable;
        }
        #endregion  



    }

}
