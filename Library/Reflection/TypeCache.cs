using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;


namespace XQuinn.Reflection;

public class DuplicateTypeNameException : Exception
{

    public DuplicateTypeNameException(Type type, Type insert, string name) : base($"Key {name} has already been cached, already-cached type is: {type.FullName}, you attempted to insert type {insert.FullName}. You can use a typebook filter to change the registered name of this type before insertion.")
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
        Cache = _registry.AsReadOnly();
    }

    public static bool Contains(string name) => _registry.ContainsKey(name);
    public static bool TryGetType(string name, out Type? cachedtype) => _registry.TryGetValue(name, out cachedtype);
    public static Type? GetTypeCached(string name, TypeBook? book = null)
    {
        if (book?.TryGetValue(name, out Type? booktype) ?? false)
            return booktype;
        if (TryGetType(name, out Type? cachedtype))
            return cachedtype;
        return null;
    }
    public static Type GetTypeOrThrow(string name, TypeBook? book = null)
    {
        Type? type = GetTypeCached(name, book);
        return type ?? throw new ArgumentException($"Could not find type named {name}.");
    }
    public static void CacheType(string name, Type type)
    {
        if (TryGetType(name, out Type? value))
        {
            if (type != value)
                throw new DuplicateTypeNameException(value!, type, name);
            else
                return;
        }
        _registry.TryAdd(name, type);
    }
    public static void CacheType(TypeBook book)
    {
        foreach (var pair in book)
        {
            if (TryGetType(pair.Key, out Type? cachedtype))
            {
                if (pair.Value != cachedtype) //if system detects duplicate names, it only throws if the types arent equal
                    throw new DuplicateTypeNameException(cachedtype!, pair.Value, pair.Key);
                else
                    continue;
            }
            _registry.TryAdd(pair.Key, pair.Value);
        }
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
    /// <summary>
    /// Add types to the quick lookup cache.
    /// </summary>
    /// <param name="types"></param>
    /// <param name="fullname"></param>

}