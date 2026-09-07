using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System;
using System.Collections.Generic;
using HarmonyLib;
using XQuinn.Parsing;
using XQuinn.CodeAnalysis;
using XQuinn.Extensions;
using System.Text;
using System.Collections;
using XQuinn.Runtime;
using System.Runtime.InteropServices;
using XQuinn.ObjectModel;
using XQuinn.IO;
using System.Runtime.CompilerServices;
using System.Linq;
using XQuinn.Private;
using XQuinn.CodeAnalysis.AST;
using XQuinn.Private.EqualityHelpers;
using System.ComponentModel;


namespace XQuinn.Reflection
{

    public class DuplicateKeyException : Exception
    {
        internal DuplicateKeyException(Type type, Type insert, TypeKey name) : this(type, insert, name.ToString())
        {

        }
        internal DuplicateKeyException(Type type, Type insert, string name) : base($"Conflict detected when trying to cache {insert.FullName} with key {name}. Key has already been used for type {type.FullName}. Key names are not case sensitive.")
        {

        }
    }

    public static class TypeCache
    {

        //    public static readonly IReadOnlyDictionary<GenericKey, Type> GlobalCache;
        public static IEnumerable<string> Keys()
        {
            foreach (TypeKey key in s_registry.Keys)
                yield return key.ToString();
        }
        public static ICollection<Type> Values => s_registry.Values;
        public static IEnumerable<KeyValuePair<string, Type>> Enumerate()
        {
            foreach (KeyValuePair<TypeKey, Type> obj in s_registry)
                yield return new(obj.Key.ToString(), obj.Value);
        }


        //Auto-Caching/Name generators (GetCompatibleName() usage)
        /// Generic types will have their generic arguments snipped off.
        /// Generic type definitions will be cached with 'T' at the end and the number of args.
        /// Ex. GameObject<string> is cached as gameobject, GameObject<T> will cache as GameObjectT1

        /// Exceptions to this are pre-cached generic type definitiosn like tuples and collections. Tuples only have their generic argument count at the end ex. tuple6,
        /// some generic collections also are not cached with T at the end, none are cached with number of args -
        ///  such as list<T> being cached as list, or dictionary<k,v> being cached as dictionary,
        /// compared to icollection<T> which is cached as icollectionT, and ICollection which is cached as icollection

        /// Array versions of types (cached using arraygen) will use short or fullname with a [] tacked on the end. You can also choose to pick your own key.
        ///  If you let arraygen create the key, it will automatically be snipped using GetCompatibleName
        /// If you are caching many types at once your should filter your names through GetCompatibleName, because default generic names and nested names (Short or full) are incompatible
        /// and will throw exceptions. Rule of thumb: alphanumerics and underscores only, do not start with a digit, and [] is allowed but good practice is to reserve that for array types.
        internal static readonly ConcurrentDictionary<TypeKey, Type> s_registry = new()
        {
            [new("object")] = typeof(object), ///Keyword types
            [new("string")] = typeof(string),
            [new("bool")] = typeof(bool),
            [new("byte")] = typeof(byte),
            [new("sbyte")] = typeof(sbyte), ///Array versions of the keyword types you see here are pre-cached, generated at runtime automatically
            [new("char")] = typeof(char),
            [new("int")] = typeof(int),
            [new("uint")] = typeof(uint),
            [new("short")] = typeof(short),
            [new("ushort")] = typeof(ushort),
            [new("long")] = typeof(long),
            [new("ulong")] = typeof(ulong),
            [new("float")] = typeof(float),
            [new("double")] = typeof(double),
            [new("decimal")] = typeof(decimal),
            [new("nint")] = typeof(nint),
            [new("nuint")] = typeof(nuint),

            [new(nameof(Enum))] = typeof(Enum),
            [new("Tuple")] = typeof(ValueTuple),
            [new(nameof(BindingFlags))] = typeof(BindingFlags),

            [new(nameof(Types))] = typeof(Types),
            // ["arraygen"] = typeof(ArrayGen),
            [new(nameof(InstanceReader))] = typeof(InstanceReader),
#if NET6_0_OR_GREATER
            [new(nameof(ILReader))] = typeof(ILReader),
#endif
            [new("typecache")] = typeof(TypeCache),
            //  ["type"] = typeof(Type),
            [new(nameof(Assembly))] = typeof(Assembly),
            [new(nameof(Activator))] = typeof(Activator),
            // [new(nameof(Convert))] = typeof(Convert),

            [new(nameof(IDisposable))] = typeof(IDisposable),

            [new(nameof(Environment))] = typeof(Environment),
            [new(nameof(AppDomain))] = typeof(AppDomain),
            [new(nameof(AppContext))] = typeof(AppContext),
            [new(nameof(RuntimeEnvironment))] = typeof(RuntimeEnvironment),
            [new(nameof(RuntimeInformation))] = typeof(RuntimeInformation),


            [new(nameof(Array))] = typeof(Array),
            [new(nameof(List<_>), 1)] = typeof(List<>),
            [new(nameof(IList), 1)] = typeof(IList<>),
            [new(nameof(IList))] = typeof(IList),
            [new(nameof(Enumerable))] = typeof(Enumerable),
            [new(nameof(IEnumerable))] = typeof(IEnumerable),
            [new(nameof(IEnumerable<_>), 1)] = typeof(IEnumerable<>),
            [new(nameof(Dictionary<_, _>), 2)] = typeof(Dictionary<,>),
            [new(nameof(IDictionary<_, _>), 2)] = typeof(IDictionary<,>),
            [new(nameof(IDictionary))] = typeof(IDictionary),
            [new("KVP", 2)] = typeof(KeyValuePair<,>),
            [new(nameof(HashSet<_>), 1)] = typeof(HashSet<>),
            [new(nameof(Collection<_>), 1)] = typeof(Collection<>),
            [new(nameof(ICollection))] = typeof(ICollection),
            [new(nameof(ICollection<_>), 1)] = typeof(ICollection<>)

        };


        static TypeCache()
        {
            //  GlobalCache = new ReadOnlyDictionary<GenericKey, Type>(s_registry);
            Assembly mscorlib = Assembly.Load("System.Private.CoreLib");
            Type runtimeType = mscorlib.GetType("System.RuntimeType", true)!;
            s_registry[new(nameof(Type))] = runtimeType; //Type doesnt really exist at Runtime, and there is an issue where the methods of Type and RuntimeType are order-swapped
                                                         //so its hard to tell what overload indexes are by checking Type, you have to load an instance of RuntimeType first
                                                         //thus we fix this problem by replacing Type with RuntimeType
                                                         // string[] keywordTypes = new[]
                                                         //  { "object", "string", "bool", "byte", "sbyte", "char", "int", "uint", "short", "ushort", "ulong", "long", "float", "decimal", "double", "nint", "nuint" };
                                                         // foreach (string keyword in keywordTypes)
                                                         //     s_registry[$"{keyword}[]"] = s_registry[keyword].MakeArrayType();
        }

        public static bool Contains(string name) => s_registry.ContainsKey(TypeKey.Query(name));
        public static bool TryGetType(string name, out Type? cachedtype) => s_registry.TryGetValue(TypeKey.Query(name), out cachedtype);

        public static Type? GetTypeCached(string name)//IReadOnlyDictionary<string, Type>? book = null)
        {
            // if (book?.TryGetValue(name, out Type? booktype) ?? false)
            //   return booktype;
            if (TryGetType(name, out Type? cachedtype))
                return cachedtype;
            return null;
        }
        public static Type GetTypeOrThrow(string name)//ReadOnlyDictionary<string, Type>? book = null)
        {
            return GetTypeCached(name) ?? throw new ArgumentException($"Could not find cached type with key {name}.");
        }

        internal static Type GetTypeOrThrow(TypeKey key)
        {
            if (s_registry.TryGetValue(key, out Type? cachedType))
                return cachedType;
            throw new ArgumentException($"Could not find cached type with key {key.Key}.");
        }

        internal static Type GetTypeOrThrow(TypeString name)
        {
            return GetTypeOrThrow(new TypeKey(name));

        }
        /// <summary>
        /// returns false if type is already cached with the same key, true if caching was performed
        /// </summary>
        /// <param name="key"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        ///


        public static bool CacheType(Type type, string key)
        {
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), true) || TypeBook.IsFileType(type))
                return false;
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                throw new NotSupportedException($"Generic type {type} with key {key} cannot be cached. Only generic type definitions and nongeneric types can be cached.");
            ThrowIfBadKey(key);
            TypeKey trueKey = new(key, type);
            if (CheckDuplicateOrCached(type, trueKey))
                return false;
            s_registry.TryAdd(trueKey, type);
            return true;
        }
        public static bool CacheType<T>(string key)
        {
            return CacheType(typeof(T), key);
        }
        public static bool CacheType<T>(bool fullname)
        {
            return CacheType(typeof(T), fullname);
        }
        public static bool CacheType(Type type, bool fullname)
        {
            return CacheType(type, GetCompatibleName(type, fullname));
        }
        public static void CacheTypes(IEnumerable<Type> types, bool fullname)
        {
            foreach (Type type in types)
                CacheType(type, fullname);

        }

        public static void CacheTypes(IEnumerable<Type> types, Func<Type, string?> keyProvider)
        {
            foreach (Type type in types)
            {
                string? key = keyProvider.Invoke(type);
                if (key != null)
                    CacheType(type, key);
            }
        }

        public static string GetCompatibleName(Type type, bool fullname)
        {
            string name = fullname ? type.FullName ?? throw new ArgumentNullException(nameof(fullname), $"Type {type} returned null for fullname.") : type.Name;
            if (type.IsGenericTypeDefinition)
                return SnipGenericName(name).ToString();
            else if (type.IsNested && fullname)
                return name.Replace('+', '.');
            else
                return name;


        }

        static StringBuilder SnipGenericName(string name)
        {
            StringBuilder sb = new();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '+')
                    c = '.';
                else if (c == '`')
                    break;
                sb.Append(c);
            }
            return sb;
        }
        //Not sure if this should be a thing... its mostly so that keys can work with invocationlexer and callinterp. typecache was pretty much made *for* callinterp so not a problem imo
        internal static void ThrowIfBadKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Empty key.");
            if (key[0].IsDigit())
                throw new ArgumentException($"Keys cannot begin with a digit. Bad Key: {key}");
            if (key[0] == '.')
                throw new ArgumentException($"Keys cannot begin with a period. Bad Key {key}");
            bool accessor = false;
            // int? skip = null;
            if (key.EqualsCaseless("base") || key.EqualsCaseless("this"))
                throw new ArgumentException($"This key is restricted and cannot be registered. Bad Key {key}.");
            // if (key.Length >= 2)
            // {

            //     if (key.Length >= 3)
            //     {
            //         int finalIndex = key.Length - 1;
            //         int beforeFinalIndex = finalIndex - 1;
            //         (char beforeFinal, char final) last = (key[beforeFinalIndex], key[finalIndex]);
            //         if (last == ('[', ']'))
            //             skip = beforeFinalIndex;
            //     }
            // }
            for (int i = 0; i < key.Length; i++)
            {
                // if (skip == i)
                //   break;
                char value = key[i];
                if (!value.IsDigit() && !value.IsLetter() && value != '_')
                {
                    if (!accessor && value == '.')
                        accessor = true;
                    else
                        throw new ArgumentException($"Keys can only consist of digits, letters, underscores, or single periods between names. Bad Key {key}. If you are having trouble caching generics, use TypeExtensions.SnipGenericName.");
                }
                else if (accessor)
                {
                    if (!InvokeLexer.ValidIdentifierFirstChar(value))
                        throw new ArgumentException($"Member access must be followed by an underscore or a letter for namespaces. Bad Key: {key}");
                    accessor = false;
                }

            }

        }


        static bool CheckDuplicateOrCached(Type type, TypeKey key)
        {
            if (s_registry.TryGetValue(key, out Type? cachedtype))
                return type == cachedtype ? true : throw new DuplicateKeyException(cachedtype!, type, key);
            return false;
        }



        /// <summary>
        /// Make a nested or generic name compatible with the cache.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fullname"></param>
        /// <param name="snipgenerics"></param>
        /// <returns></returns>



        static class Types
        {
            public static IEnumerable<string> Fields<T>(string? contains = null, BindingFlags search = Navigator.Flag) => Fields(typeof(T), contains, search);
            public static IEnumerable<string> Overloads<T>(string? contains = null, BindingFlags search = Navigator.Flag) => Overloads(typeof(T), contains, search);
            public static IEnumerable<string> Methods<T>(string? contains = null, BindingFlags search = Navigator.Flag) => Methods(typeof(T), contains, search);

            ///Send GetType as your Type Parameter if your instance's type is not in the cache
            /// 
            public static IEnumerable<string> Properties(Type t, string? contains = null)//BindingFlags search = Navigator.Flag)
            {
                IEnumerable<KeyValuePair<string, PropertyInfo>> props = t.GetProperties(Navigator.Flag).Select(x => new KeyValuePair<string, PropertyInfo>(x.Name, x));
                return ReadMembers(props, contains, Navigator.Flag);
            }
            public static IEnumerable<string> Fields(Type t, string? contains = null, BindingFlags search = Navigator.Flag)
            {
                IEnumerable<KeyValuePair<string, FieldInfo>> fields = t.GetFields(Navigator.Flag).Select(x => new KeyValuePair<string, FieldInfo>(x.Name, x));
                return ReadMembers(fields, contains, search);
            }
            public static IEnumerable<string> Overloads(Type t, string? contains = null, BindingFlags search = Navigator.Flag)
            {
                Dictionary<Navigator.ResolvedOverload, MethodBase> overloads = new();
                Navigator.MapType(null, overloads, null, t, null, contains?.EqualsCaseless("new") ?? true);
                return ReadMembers(overloads, contains, search);
            }


            public static IEnumerable<string> Methods(Type t, string? contains = null, BindingFlags search = Navigator.Flag)
            {
                Dictionary<string, MethodBase> methods = new();
                Navigator.MapType(methods, null, null, t, null, contains?.EqualsCaseless("new") ?? true);
                return ReadMembers(methods, contains, search);
            }


            public static void FlushStaticCache(bool ambiguousMatches, bool accessedMembers, bool reifiedGenerics)
            {
                Navigator.FlushStaticCache(ambiguousMatches, accessedMembers, reifiedGenerics);
            }

            // public static T Cast<T>(object obj)
            // {
            //     return (T)Convert.ChangeType(obj, typeof(T));
            // }
            public static Type Of<T>() => typeof(T);
            public static Type Of(string name) => GetTypeOrThrow(name); //for generic definitions which cant be passed as T without their own typeargs
            public static T Struct<T>(T obj = default) where T : unmanaged => obj;
            public static T Enum<T>(T obj) where T : Enum => obj;
            public static string String(string txt) => txt; //only way to instantiate an isolated new string using the navigator
            public static T[] Array<T>(params T[] arr) => arr.Length == 0 ? System.Array.Empty<T>() : arr;
            public static T[] Array<T>(int i) => i == 0 ? System.Array.Empty<T>() : new T[i];
            public static bool LateCache(string assemblyName, string targetTypeName, string keyForCaching)
            {
                Assembly assembly = Assembly.Load(assemblyName);
                Type targetType = assembly.GetType(targetTypeName, false, true) ?? throw new ArgumentException($"No type found in assembly {assembly.FullName} named {targetTypeName}. Requires full name.");
                return CacheType(targetType, keyForCaching);

            }
            // public static bool LateCache<T>(string targetTypeName, string keyForCaching)
            // {
            //     return LateCache(typeof(T), targetTypeName, keyForCaching);
            // }
            // public static bool LateCache(Type cachedType, string targetTypeName, string keyForCaching)
            // {
            //     return LateCache(cachedType.Assembly, targetTypeName, keyForCaching);
            // }
            // public static bool LateCache(Assembly assembly, string targetTypeName, string keyForCaching)
            // {
            //     Type targetType = assembly.GetType(targetTypeName, false, true) ?? throw new ArgumentException($"No type found in assembly {assembly.FullName} named {targetTypeName}. Requires full name.");
            //     return CacheType(keyForCaching, targetType);
            // }

            static IEnumerable<string> ReadMembers<K, V>(IEnumerable<KeyValuePair<K, V>> members, string? key, BindingFlags flags) where V : MemberInfo
            {
                if (key != null)
                {
                    bool contained = false;
                    foreach (KeyValuePair<K, V> member in members)
                    {
                        if (SearchModifiers.ProcessSearch(member.Value, flags) && member.Key!.ToString()!.ContainsCaseless(key))
                        {
                            contained = true;
                            yield return $"[Key: {member.Key} :: {ReflectionPrinter.Print(member.Value)}]";
                        }
                    }
                    if (!contained)
                        yield return $"No {typeof(V).Name} found with name containing {key} with search option {flags}.";
                }
                else
                    foreach (KeyValuePair<K, V> member in members)
                        if (SearchModifiers.ProcessSearch(member.Value, flags))
                            yield return $"[Key: {member.Key} :: {ReflectionPrinter.Print(member.Value)}]";
            }



            readonly struct SearchModifiers
            {
                readonly bool _static;
                readonly bool _public;
                readonly bool _inherited;

                public static bool ProcessSearch(MemberInfo inf, BindingFlags flags)
                {
                    if (inf is MethodBase mthd)
                        return new SearchModifiers(mthd).ProcessSearch(flags);
                    if (inf is FieldInfo field)
                        return new SearchModifiers(field).ProcessSearch(flags);
                    return true;
                }

                public readonly bool ProcessSearch(BindingFlags flags)
                {
                    if (_inherited)
                    {
                        if (flags.HasFlag(BindingFlags.DeclaredOnly))
                            return false;
                    }
                    if (_public)
                    {
                        if (!flags.HasFlag(BindingFlags.Public))
                            return false;

                    }
                    else
                    {
                        if (!flags.HasFlag(BindingFlags.NonPublic))
                            return false;
                    }
                    if (_static && _inherited)
                        return flags.HasFlag(BindingFlags.FlattenHierarchy);
                    if (_static)
                        return flags.HasFlag(BindingFlags.Static);
                    else
                        return flags.HasFlag(BindingFlags.Instance);
                }

                public SearchModifiers(MethodBase method)
                {
                    _static = method.IsStatic;
                    _public = method.IsPublic;
                    _inherited = Inherited(method);
                }

                public SearchModifiers(FieldInfo field)
                {
                    _static = field.IsStatic;
                    _public = field.IsPublic;
                    _inherited = Inherited(field);
                }

                //                  public SearchModifiers(PropertyInfo prop)
                //                 {
                //                     _static = prop.GetGetMethod(true) is {IsStatic:true};
                //                     _inherited = Inherited(prop);
                //                 }
                static bool Inherited(MemberInfo obj)
                {
                    if (obj.DeclaringType != null)
                        return obj.ReflectedType != obj.DeclaringType;
                    return false;
                }

            }

        }
        // static class ArrayGen
        // {

        //     // public static T[] New<T>(params T[] arr) => arr;

        //     // public static T[] New<T>(int i) => new T[i];

        //     // public static bool GenerateCachedArray<T>(string name)
        //     // {
        //     //     return GenerateCachedArray(typeof(T[]), name);
        //     // }
        //     // public static bool GenerateCachedArray(Type t, string name)
        //     // {
        //     //     Type array = t.IsArray ? t : t.MakeArrayType();
        //     //     return TypeCache.CacheType(name, array);

        //     }

        //     // public static string GenerateCachedArray<T>(bool fullname)
        //     // {
        //     //     return GenerateCachedArray(typeof(T[]), fullname);
        //     // }
        //     //Just put your own name for now
        //     // public static string GenerateCachedArray(Type t, bool fullname)
        //     // {
        //     //     Type array = t.IsArray ? t : t.MakeArrayType();
        //     //     string name;
        //     //     if (t.IsArray)
        //     //     {
        //     //         Type underlying = t.GetElementType()!;
        //     //         name = $"{TypeCache.GetCompatibleName(underlying, fullname)}[]";
        //     //     }
        //     //     else name = $"{TypeCache.GetCompatibleName(t, fullname)}[]";
        //     //     TypeCache.CacheType(name, array);
        //     //     return name;
        //     // }

        // }



        class _ { }
    }
}