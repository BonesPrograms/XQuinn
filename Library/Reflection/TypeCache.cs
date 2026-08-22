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
        public static ICollection<Type> Types => _registry.Values;
        public static IEnumerable<KeyValuePair<string, Type>> Enumerate()
        {
            foreach (var obj in _registry) yield return obj;
        }
        static readonly ConcurrentDictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase)
        {
            ["void"] = typeof(void),
            ["object"] = typeof(object),
            ["string"] = typeof(string),
            ["bool"] = typeof(bool),
            ["byte"] = typeof(byte),
            ["sbyte"] = typeof(sbyte),
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
            ["type"] = typeof(Type),
            ["typecache"] = typeof(TypeCache),
            ["environment"] = typeof(Environment),
            ["assembly"] = typeof(Assembly),
            ["appdomain"] = typeof(AppDomain),
            ["array"] = typeof(Array),
            ["list"] = typeof(List<>),
            ["ilistT"] = typeof(IList<>),
            ["ilist"] = typeof(IList),
            ["enumerable"] = typeof(IEnumerable),
            ["enumerableT"] = typeof(IEnumerable<>),
            ["dictionary"] = typeof(Dictionary<,>),
            ["idictionaryT"] = typeof(IDictionary<,>),
            ["idicitonary"] = typeof(IDictionary),
            ["hashset"] = typeof(HashSet<>)

            // ["stringbuilder"] = typeof(StringBuilder) //maybe will add readonly variants of collections later
        };

        static readonly string[] BadKeys = new string[] { "this", "null", "base", "default" };
        static TypeCache()
        {
            GlobalCache = new ReadOnlyDictionary<string, Type>(_registry);
            foreach (var pair in _registry) ///Generate array types for primitives, string and object
                if (pair.Value.IsPrimitive || pair.Value == typeof(string) || pair.Value == typeof(object))
                    CacheType($"{pair.Key}[]", pair.Value.MakeArrayType());
            foreach(Type t in Assembly.GetAssembly(typeof(ValueTuple)).GetTypes()) /// Generate cache types for all possible value tuples
            {
                if(t.Name.Contains("ValueTuple"))
                {
                    if(t.Name == "IValueTupleInternal" || t.Name == "ITuple") continue;
                    int args = t.GetGenericArguments().Length;
                    string name = args == 0 ? "tuple" : $"tuple{args}";
                    CacheType(name, t);
                }
            }

        }

        public static bool Contains(string name) => _registry.ContainsKey(name);
        public static bool TryGetType(string name, out Type? cachedtype) => _registry.TryGetValue(name, out cachedtype);

        public static string GenerateCachedArray<T>(bool fullname)
        {
            return GenerateCachedArray(typeof(T), fullname);
        }

        public static string GenerateCachedArray(Type t, bool fullname)
        {
            Type array = t.IsArray ? t : t.MakeArrayType();
            string name;
            if (t.IsArray)
            {
                Type underlying = t.GetElementType()!;
                name = $"{GetCompatibleName(underlying, fullname)}[]";
            }
            else name = $"{GetCompatibleName(t, fullname)}[]";
            CacheType(name, array);
            return name;
        }
        public static Type GenerateCachedArray<T>(string name)
        {
            return GenerateCachedArray(typeof(T[]), name);
        }
        public static Type GenerateCachedArray(Type t, string name)
        {
            Type array = t.IsArray ? t : t.MakeArrayType();
            CacheType(name, array);
            return array;
        }
        public static Type? GetTypeCached(string name, IReadOnlyDictionary<string, Type>? book = null)
        {
            if (book?.TryGetValue(name, out Type? booktype) ?? false) return booktype;
            else if (TryGetType(name, out Type? cachedtype)) return cachedtype;
            else return null;
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

            ThrowIfBadKey(key);
            if (CheckDuplicateOrCached(key, type)) return false;
            else _registry.TryAdd(key, type);
            return true;
        }

        //Not sure if this should be a thing... its mostly so that keys can work with invocationlexer and callinterp. typecache was pretty much made *for* callinterp so not a problem imo
        public static void ThrowIfBadKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Empty key.");
            if (key[0].IsDigit()) throw new ArgumentException($"Keys cannot begin with a digit. Bad Key: {key}");
            if (key[0] == '.') throw new ArgumentException($"Keys cannot begin with a period. Bad Key{key}");
            for (int i = 0; i < BadKeys.Length; i++) if (key.EqualsCaseless(BadKeys[i])) throw new ArgumentException($"This key is restricted and cannot be registered. Bad Key {key}.");
            bool accessor = false;
            for (int i = 0; i < key.Length; i++)
            {
                char value = key[i];
                if (!value.IsDigit() && !value.IsLetter() && value != '_' && value != '[' && value != ']')
                {
                    if (!accessor && value == '.') accessor = true;
                    else throw new ArgumentException($"Keys can only consist of digits, letters, underscores, [] array brackets, or single periods between names. Bad Key {key}. If you are having trouble caching generics, use TypeExtensions.SnipGenericName.");
                }
                else if (accessor) accessor = false;

            }

        }

        public static void CacheTypes(IEnumerable<KeyValuePair<string, Type>> book)
        {
            foreach (var pair in book) CacheType(pair.Key, pair.Value);
        }

        static bool CheckDuplicateOrCached(string Key, Type Value)
        {
            if (TryGetType(Key, out Type? cachedtype)) return Value == cachedtype ? true : throw new DuplicateKeyException(cachedtype!, Value, Key);
            else return false;
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
            if (type.IsNested)
            {
                if (type.IsGenericType)
                {
#if NET6_0_OR_GREATER
                    ReadOnlySpan<char> span = type.SnipGenericName(fullname);
                    Span<char> buffer = stackalloc char[span.Length];
                    span.CopyTo(buffer);
                    for (int i = 0; i < buffer.Length; i++) if (buffer[i] == '+') buffer[i] = '.';
                    return buffer.ToString();
#else
                 return type.SnipGenericName(fullname).Replace('+','.').ToString();
#endif
                }
                else return fullname ? type.FullName.Replace('+', '.') : type.Name.Replace('+', '.');
            }
            else if (type.IsGenericType) return type.SnipGenericName(fullname).ToString();
            else return fullname ? type.FullName : type.Name;
        }

    }
}