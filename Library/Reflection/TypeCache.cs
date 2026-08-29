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


namespace XQuinn.Reflection
{

    public class DuplicateKeyException : Exception
    {

        public DuplicateKeyException(Type type, Type insert, string name) : base($"Conflict detected when trying to cache {insert.FullName} with key {name}. Key has already been used for type {type.FullName}. Key names are not case sensitive.")
        {

        }
    }
    public static class TypeCache
    {

        public static readonly IReadOnlyDictionary<string, Type> GlobalCache;
        public static ICollection<string> Keys => _registry.Keys;
        public static ICollection<Type> Values => _registry.Values;
        public static IEnumerable<KeyValuePair<string, Type>> Enumerate()
        {
            foreach (var obj in _registry)
                yield return obj;
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
        static readonly ConcurrentDictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase)
        {
            ["object"] = typeof(object), ///Keyword types
            ["string"] = typeof(string),
            ["bool"] = typeof(bool),
            ["byte"] = typeof(byte),
            ["sbyte"] = typeof(sbyte), ///Array versions of the keyword types you see here are pre-cached, generated at runtime automatically
            ["char"] = typeof(char),
            ["int"] = typeof(int),
            ["uint"] = typeof(uint),
            ["short"] = typeof(short),
            ["ushort"] = typeof(ushort),
            ["long"] = typeof(long),
            ["ulong"] = typeof(ulong),
            ["float"] = typeof(float),
            ["double"] = typeof(double),
            ["decimal"] = typeof(decimal),
            ["nint"] = typeof(nint),
            ["nuint"] = typeof(nuint),

            ["enum"] = typeof(Enum),
            ["tuple"] = typeof(ValueTuple),
            ["bindingflags"] = typeof(BindingFlags),

            ["types"] = typeof(Types),
            ["arraygen"] = typeof(ArrayGen),
            ["instancereader"] = typeof(InstanceReader),
#if NET6_0_OR_GREATER                                 
            ["ilreader"] = typeof(ILReader),
#endif

            ["type"] = typeof(Type),
            ["assembly"] = typeof(Assembly),
            ["activator"] = typeof(Activator),
            ["convert"] = typeof(Convert),

            ["environment"] = typeof(Environment),
            ["appdomain"] = typeof(AppDomain),
            ["appcontext"] = typeof(AppContext),
            ["runtimeenvironment"] = typeof(RuntimeEnvironment),
            ["runtimeinformation"] = typeof(RuntimeInformation),


            ["array"] = typeof(Array),
            ["list"] = typeof(List<>),
            ["ilistT"] = typeof(IList<>),
            ["ilist"] = typeof(IList),
            ["enumerable"] = typeof(System.Linq.Enumerable),
            ["ienumerable"] = typeof(IEnumerable),
            ["ienumerableT"] = typeof(IEnumerable<>),
            ["dictionary"] = typeof(Dictionary<,>),
            ["idictionaryT"] = typeof(IDictionary<,>),
            ["idictionary"] = typeof(IDictionary),
            ["keyvaluepair"] = typeof(KeyValuePair<,>),
            ["hashset"] = typeof(HashSet<>),
            ["collection"] = typeof(Collection<>),
            ["icollection"] = typeof(ICollection),
            ["icollectionT"] = typeof(ICollection<>)

        };


        static TypeCache()
        {
            GlobalCache = new ReadOnlyDictionary<string, Type>(_registry);
            string[] keywordTypes = new[]
             { "object", "string", "bool", "byte", "sbyte", "char", "int", "uint", "short", "ushort", "ulong", "long", "float", "decimal", "double", "nint", "nuint" };
            foreach (string keyword in keywordTypes)
                _registry[$"{keyword}[]"] = _registry[keyword].MakeArrayType();
        }

        public static bool Contains(string name) => _registry.ContainsKey(name);
        public static bool TryGetType(string name, out Type? cachedtype) => _registry.TryGetValue(name, out cachedtype);

        public static Type? GetTypeCached(string name, IReadOnlyDictionary<string, Type>? book = null)
        {
            if (book?.TryGetValue(name, out Type? booktype) ?? false)
                return booktype;
            if (TryGetType(name, out Type? cachedtype))
                return cachedtype;
            return null;
        }
        public static Type GetTypeOrThrow(string name, IReadOnlyDictionary<string, Type>? book = null)
        {
            return GetTypeCached(name, book) ?? throw new ArgumentException($"Could not find cached type with key {name}.");
        }
        /// <summary>
        /// returns false if type is already cached with the same key, true if caching was performed
        /// </summary>
        /// <param name="key"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool CacheType(string key, Type type)
        {
            return CacheTypeInternal(key, type, true);
        }

        static bool CacheTypeInternal(string key, Type type, bool checkForGeneratedOrFileType)
        {
            if (checkForGeneratedOrFileType && (type.IsDefined(typeof(CompilerGeneratedAttribute)) || TypeBook.IsFileType(type)))
                return false;
            ThrowIfBadKey(key);
            if (CheckDuplicateOrCached(key, type))
                return false;
            _registry.TryAdd(key, type);
            return true;
        }

        public static bool CacheType<T>(string key)
        {
            return CacheType(key, typeof(T));
        }

        public static bool CacheType(Type type, bool fullname)
        {
            return CacheType(GetCompatibleName(type, fullname), type);
        }

        public static bool CacheType<T>(bool fullname)
        {
            return CacheType(typeof(T), fullname);
        }

        public static void CacheTypes(TypeBook book)
        {
            foreach (var pair in book)
                CacheTypeInternal(pair.Key, pair.Value, false);
        }
        public static void CacheTypes(IEnumerable<Type> types, bool fullname)
        {
            foreach (Type type in types)
                CacheType(type, fullname);

        }

        public static void CacheTypes(IEnumerable<KeyValuePair<string, Type>> book)
        {
            foreach (var pair in book)
                CacheType(pair.Key, pair.Value);
        }
        public static string GetCompatibleName(Type type, bool fullname)
        {
            string name = fullname ? type.FullName ?? throw new ArgumentNullException(nameof(fullname), $"Type {type} returned null for fullname.") : type.Name;
            if (type.IsGenericTypeDefinition)
                return GenericToString(name, type.GetGenericArguments().Length, fullname, type.IsNested);
            else if (type.IsNested && fullname)
                return name.Replace('+', '.');
            else
                return name;
        }


        //Not sure if this should be a thing... its mostly so that keys can work with invocationlexer and callinterp. typecache was pretty much made *for* callinterp so not a problem imo
        internal static void ThrowIfBadKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Empty key.");
            if (key[0].IsDigit())
                throw new ArgumentException($"Keys cannot begin with a digit. Bad Key: {key}");
            if (key[0] == '.')
                throw new ArgumentException($"Keys cannot begin with a period. Bad Key{key}");
            bool accessor = false;
            int? skip = null;
            if (key.Length >= 2)
            {
                if (key.EqualsCaseless("base") || key.EqualsCaseless("this"))
                    throw new ArgumentException($"This key is restricted and cannot be registered. Bad Key {key}.");
                if (key.Length >= 3)
                {
                    int finalIndex = key.Length - 1;
                    int beforeFinalIndex = finalIndex - 1;
                    (char beforeFinal, char final) last = (key[beforeFinalIndex], key[finalIndex]);
                    if (last == ('[', ']'))
                        skip = beforeFinalIndex;
                }
            }
            for (int i = 0; i < key.Length; i++)
            {
                if (skip == i)
                    break;
                char value = key[i];
                if (!value.IsDigit() && !value.IsLetter() && value != '_')
                {
                    if (!accessor && value == '.')
                        accessor = true;
                    else
                        throw new ArgumentException($"Keys can only consist of digits, letters, underscores, [] array brackets at the end, or single periods between names. Bad Key {key}. If you are having trouble caching generics, use TypeExtensions.SnipGenericName.");
                }
                else if (accessor)
                    accessor = false;

            }

        }


        static bool CheckDuplicateOrCached(string Key, Type Value)
        {
            if (TryGetType(Key, out Type? cachedtype))
                return Value == cachedtype ? true : throw new DuplicateKeyException(cachedtype!, Value, Key);
            return false;
        }
        /// <summary>
        /// Make a nested or generic name compatible with the cache. 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fullname"></param>
        /// <param name="snipgenerics"></param>
        /// <returns></returns>

        static string GenericToString(string name, int genericargs, bool fullname, bool nested)
        {
            StringBuilder snippedname = SnipGenericName(name);
            if (fullname && nested)
                snippedname.Replace('+', '.');
            snippedname.Append('T');
            if (genericargs > 0)
                snippedname.Append(genericargs);
            return snippedname.ToString();
        }
        static StringBuilder SnipGenericName(string name)
        {
            StringBuilder sb = new();
            foreach (char c in name)
                if (c == '`')
                    break;
                else sb.Append(c);
            return sb;
        }

        static class Types
        {

            public static void FlushStaticCache(bool ambiguousMatches, bool accessedMembers, bool reifiedGenerics)
            {
                Navigator.FlushStaticCache(ambiguousMatches, accessedMembers, reifiedGenerics);
            }
            public static Type Of<T>() => typeof(T);
            public static Type Of(string x) => GetTypeOrThrow(x);
            public static T New<T>() where T : new() => new();
            public static T New<T>(T obj) => obj;
            public static bool LateCache(string assemblyName, string targettypeName, string keyForCaching)
            {
                Assembly assembly = Assembly.Load(assemblyName);
                return LateCache(assembly, targettypeName, keyForCaching);

            }
            public static bool LateCache<T>(string targetTypeName, string keyForCaching)
            {
                return LateCache(typeof(T), targetTypeName, keyForCaching);
            }
            public static bool LateCache(Type cachedType, string targetTypeName, string keyForCaching)
            {
                return LateCache(cachedType.Assembly, targetTypeName, keyForCaching);
            }
            public static bool LateCache(Assembly assembly, string targetTypeName, string keyForCaching)
            {
                Type targetType = assembly.GetType(targetTypeName, false, true) ?? throw new ArgumentException($"No type found in assembly {assembly.FullName} named {targetTypeName}. Requires full name.");
                return CacheType(keyForCaching, targetType);
            }
        }
        static class ArrayGen
        {

            public static T[] New<T>(params T[] arr) => arr;

            public static T[] New<T>(int i) => new T[i];

            public static bool GenerateCachedArray<T>(string name)
            {
                return GenerateCachedArray(typeof(T[]), name);
            }
            public static bool GenerateCachedArray(Type t, string name)
            {
                Type array = t.IsArray ? t : t.MakeArrayType();
                return TypeCache.CacheType(name, array);

            }

            // public static string GenerateCachedArray<T>(bool fullname)
            // {
            //     return GenerateCachedArray(typeof(T[]), fullname);
            // }
            //Just put your own name for now
            // public static string GenerateCachedArray(Type t, bool fullname)
            // {
            //     Type array = t.IsArray ? t : t.MakeArrayType();
            //     string name;
            //     if (t.IsArray)
            //     {
            //         Type underlying = t.GetElementType()!;
            //         name = $"{TypeCache.GetCompatibleName(underlying, fullname)}[]";
            //     }
            //     else name = $"{TypeCache.GetCompatibleName(t, fullname)}[]";
            //     TypeCache.CacheType(name, array);
            //     return name;
            // }

        }




    }
}