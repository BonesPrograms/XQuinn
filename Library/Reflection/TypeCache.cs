using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System;
using System.Collections.Generic;
using HarmonyLib;
using XQ.Parsing;
using XQ.CodeAnalysis;
using XQ.Extensions;
using System.Text;
using System.Collections;
using XQ.Runtime;
using System.Runtime.InteropServices;
using XQ.ObjectModel;
using XQ.IO;


namespace XQ.Reflection
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
        public static ICollection<Type> Types => _registry.Values;
        public static IEnumerable<KeyValuePair<string, Type>> Enumerate()
        {
            foreach (var obj in _registry) yield return obj;
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

            ["stringbuilder"] = typeof(StringBuilder),


            ["typecache"] = typeof(TypeCache),
            ["rtnavig"] = typeof(Navigator),
            //   ["reflectionprinter"] = typeof(ReflectionPrinter), ///XQuinn types that I consider useful for navigation
            ["arraygen"] = typeof(ArrayGen),
            ["instancereader"] = typeof(InstanceReader),
            ["tchelpers"] = typeof(CacheHelpers),
            //   ["logger"] = typeof(Logger),
            //["typebook"] = typeof(TypeBook),
            ["sbE"] = typeof(StringBuilderExtensions), ///AppendMany is particular useful in tandem with RuntimeNavigator's stringbuilder variable for printing collections at runtime
#if NET6_0_OR_GREATER                                   ///Assuming the elements themselves have a decent ToString() overload...
            ["ilreader"] = typeof(ILReader),
#endif

            ["type"] = typeof(Type),
            ["assembly"] = typeof(Assembly),
            ["activator"] = typeof(Activator),
            ["convert"] = typeof(Convert),
            // ["acesstoolsE"] = typeof(AccessToolsExtensions), ///Reflection based types that are useful for navigation
            // ["accesstools"] = typeof(AccessTools), 
            //   ["harmony"] = typeof(Harmony), //Rather not have a dependency rely on a single element


            ["environment"] = typeof(Environment),
            ["appdomain"] = typeof(AppDomain), ///Runtime/AppDomain types that are useful for navigation
            ["appcontext"] = typeof(AppContext),
            ["runtimeenvironment"] = typeof(RuntimeEnvironment),
            ["runtimeinformation"] = typeof(RuntimeInformation),


            ["array"] = typeof(Array),
            ["list"] = typeof(List<>),
            ["ilistT"] = typeof(IList<>),           ///Collections and their necessities
            ["ilist"] = typeof(IList),
            ["enumerable"] = typeof(System.Linq.Enumerable),
            ["ienumerable"] = typeof(IEnumerable),
            ["ienumerableT"] = typeof(IEnumerable<>),
            ["dictionary"] = typeof(Dictionary<,>),
            ["idictionaryT"] = typeof(IDictionary<,>),
            ["idictionary"] = typeof(IDictionary),
            ["hashset"] = typeof(HashSet<>),
            ["icollection"] = typeof(ICollection),
            ["icollectionT"] = typeof(ICollection<>),
            ["keyvaluepair"] = typeof(KeyValuePair<,>),

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

        public static Type? GetType(string name, IReadOnlyDictionary<string, Type>? book = null)
        {
            if (book?.TryGetValue(name, out Type? booktype) ?? false)
                return booktype;
            else if (TryGetType(name, out Type? cachedtype))
                return cachedtype;
            else return null;
        }
        public static Type GetTypeOrThrow(string name, IReadOnlyDictionary<string, Type>? book = null)
        {
            return GetType(name, book) ?? throw new ArgumentException($"Could not find cached type with key {name ?? "key is null"}.");
        }
        /// <summary>
        /// returns false if type is already cached with the same key, true if caching was performed
        /// </summary>
        /// <param name="key"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool CacheType(string key, Type type)
        {
            ThrowIfBadKey(key);
            if (CheckDuplicateOrCached(key, type))
                return false;
            else
                _registry.TryAdd(key, type);
            return true;
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
                if (key == "[]" || key.EqualsCaseless("base") || key.EqualsCaseless("this"))
                    throw new ArgumentException($"This key is restricted and cannot be registered. Bad Key {key}.");
                if (key.Length >= 3)
                {
                    int beforeFinalIndex = key.Length - 2;
                    int finalIndex = key.Length - 1;
                    (char beforeFinal, char final) last = (key[beforeFinalIndex], key[finalIndex]);
                    skip = last == ('[', ']') ? beforeFinalIndex : null;
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
                else if (accessor) accessor = false;

            }

        }

        public static void CacheTypes(IEnumerable<KeyValuePair<string, Type>> book)
        {
            foreach (var pair in book)
                CacheType(pair.Key, pair.Value);
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
        public static string GetCompatibleName(Type type, bool fullname)
        {
            string name = fullname ? type.FullName ?? throw new ArgumentNullException(nameof(fullname), $"Type {type} returned null for fullname.") : type.Name;
            if (type.IsGenericType)
                return GenericToString(name, type.GetGenericArguments().Length, type.IsNested);
            else if (type.IsNested)
                return name.Replace('+', '.');
            else
                return name;
        }

        static string GenericToString(string name, int length, bool nested)
        {
            StringBuilder snippedname = SnipGenericName(name);
            if (nested)
                snippedname.Replace('+', '.');
            snippedname.Append('T');
            if (length > 0)
                snippedname.Append(length);
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

        /// <summary>
        /// Helper class for RuntimeNavigator. Allows you to generate cached array types, or create new arrays generically without needing to use Array or Activator methods.
        /// Not really intended or necessary for use at compile
        /// </summary>
        /// 
        static class CacheHelpers
        {

            static string NewString(string x )=> x;
            static Type Of(string x) => GetTypeOrThrow(x);
        
            static T New<T>() where T : new() => new(); //This is useful for instantiating certain kinds of structs, who's constructors are not accessible due
                                                        //to not being defined at all.

            //These methods allow you to cache new types at runtime using the navigator with ease.
            static bool LateCacheByType(string cachedTypeName, string targetTypeName, string keyForCaching)
            {
                return LateCache(TypeCache.GetTypeOrThrow(cachedTypeName).Assembly, targetTypeName, keyForCaching);
            }

            static bool LateCacheByAssembly(string assemblyName, string targettypeName, string keyForCaching)
            {
                Assembly assembly = Assembly.Load(assemblyName);
                return LateCache(assembly, targettypeName, keyForCaching);

            }
            static bool LateCache(Assembly assembly, string targetTypeName, string keyForCaching)
            {
                Type targetType = assembly.GetType(targetTypeName, false, true) ?? throw new ArgumentException($"No type found in assembly {assembly.FullName} named {targetTypeName}. Requires full name.");
                return CacheType(keyForCaching, targetType);
            }
        }
        static class ArrayGen
        {

            public static T[] New<T>(params T[] arr) => arr;

            public static T[] New<T>(int i) => new T[i];

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
            public static bool GenerateCachedArray<T>(string name)
            {
                return GenerateCachedArray(typeof(T[]), name);
            }
            public static bool GenerateCachedArray(Type t, string name)
            {
                Type array = t.IsArray ? t : t.MakeArrayType();
                return TypeCache.CacheType(name, array);

            }
        }




    }
}