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
    // [Flags]
    // public enum TypeName
    // {
    //     _invalid = 0,
    //     FullName = 8,
    //     ShortName = 16,
    //     Assorted = FullName | ShortName
    // }

    //It should be noted that the fullname for generics is funky and shortnames might be preferred in those cases, or manually made names that you insert yourself.
    //Typenames cannot be removed once cached.

    //It is recommended not to cache types from hot reloaded assemblies. Instead, make TypeBooks of those assemblies, then you can index those typebooks yourself or send
    //them to this method and it will try to index them first. If you reload your assembly and your hot reloaded types are cached here, it will cause memory leaks.
    public static class TypeCache
    {

        public static readonly IReadOnlyDictionary<string, Type> Cache;
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
            Cache = new ReadOnlyDictionary<string, Type>(_registry);
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


        // /// <summary>
        // /// Add types to the quick lookup cache. 
        // /// </summary>
        // /// <param name="types"></param>
        // /// <param name="fullname"></param>
        // public static void Register(Module m, bool fullname) => Register(m.GetTypes(), fullname);
        // /// <summary>
        // /// Add types to the quick lookup cache via assembly reference.
        // /// </summary>
        // /// <param name="types"></param>
        // /// <param name="fullname"></param>
        // public static bool TryRegister(Assembly a, bool fullname)
        // {
        //     Type[] types;
        //     try
        //     {
        //         types = a.GetTypes();
        //     }
        //     catch (ReflectionTypeLoadException ex)
        //     {
        //         var obj = ex.Types?.Where(x => x != null);
        //         if (obj != null && obj.Any())
        //         {
        //             Register(obj!, fullname);
        //             return true;
        //         }
        //         return false;
        //     }
        //     Register(types, fullname);
        //     return true;
        // }
        // /// <summary>
        // /// Add types to the quick lookup cache. 
        // /// </summary>
        // /// <param name="types"></param>
        // /// <param name="fullname"></param>
        // public static void Register(IEnumerable<Type> types, bool fullname)
        // {
        //     foreach (var type in types)
        //     {
        //         if (fullname is true && type.FullName != null)
        //         {
        //             //  if (Registry.ContainsKey(type.FullName)) //Fullnames will never match so this isnt a problem
        //             //   throw new TypeAlreadyCachedException(type, type.FullName);
        //             Registry.TryAdd(type.FullName, type);
        //         }
        //         else if (fullname is false)
        //         {
        //             if (TryGetValue(type.Name, out Type? value))
        //                 throw new ShortNameDuplicateException(value!, type.Name);
        //             Registry.TryAdd(type.Name, type);
        //         }
        //     }
        // }


    }
}