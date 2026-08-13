using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System;
using System.Collections.Generic;
using HarmonyLib;
using XQuinn.Runtime;


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
            foreach (var obj in _registry)
                yield return obj;
        }
        static readonly ConcurrentDictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase)
        {
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
            ["nuint"] = typeof(nuint)
        };

        static TypeCache()
        {
            GlobalCache = new ReadOnlyDictionary<string, Type>(_registry);
        }

        public static bool Contains(string name) => _registry.ContainsKey(name);
        public static bool TryGetType(string name, out Type? cachedtype) => _registry.TryGetValue(name, out cachedtype);
        public static Type? GetTypeCached(string name, IReadOnlyDictionary<string,Type>? book = null)
        {
            if (book?.TryGetValue(name, out Type? booktype) ?? false)
                return booktype;
            if (TryGetType(name, out Type? cachedtype))
                return cachedtype;
            return null;
        }
        public static Type GetTypeOrThrow(string name, IReadOnlyDictionary<string,Type>? book)
        {
            Type? type = GetTypeCached(name, book);
            return type ?? throw new ArgumentException($"Could not find cached type with key {name}.");
        }

        public static Type GetTypeOrThrow(string name)
        {
            Type? type = GetTypeCached(name, null);
            return type ?? throw new ArgumentException($"Could not find cached type with key {name}.");
        }
        /// <summary>
        /// returns false if type is already cached with the same key, true if caching was performed
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool CacheType(string name, Type type)
        {
            if (CheckDuplicateOrCached(name, type))
                return false;
            _registry.TryAdd(name, type);
            return true;
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

    }
}