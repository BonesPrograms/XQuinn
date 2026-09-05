using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System;
using System.Collections.Generic;
using HarmonyLib;
using XQuinn.Parsing;
using XQuinn.LexicalAnalysis;
using XQuinn.Extensions;
using System.Text;
using System.Collections;
using XQuinn.Runtime;
using System.Runtime.InteropServices;
using XQuinn.ObjectModel;
using XQuinn.IO;
using System.Runtime.CompilerServices;
using System.Linq;


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
        public static ICollection<string> Keys => s_registry.Keys;
        public static ICollection<Type> Values => s_registry.Values;
        public static IEnumerable<KeyValuePair<string, Type>> Enumerate()
        {
            foreach (var obj in s_registry)
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
        static readonly ConcurrentDictionary<string, Type> s_registry = new(StringComparer.OrdinalIgnoreCase)
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

            //["enum"] = typeof(Enum),
            ["tuple"] = typeof(ValueTuple),
            ["bindingflags"] = typeof(BindingFlags),

            ["types"] = typeof(Types),
            ["arraygen"] = typeof(ArrayGen),
            ["instancereader"] = typeof(InstanceReader),
#if NET6_0_OR_GREATER                                 
            ["ilreader"] = typeof(ILReader),
#endif
            ["typecache"] = typeof(TypeCache),
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
            GlobalCache = new ReadOnlyDictionary<string, Type>(s_registry);
            string[] keywordTypes = new[]
             { "object", "string", "bool", "byte", "sbyte", "char", "int", "uint", "short", "ushort", "ulong", "long", "float", "decimal", "double", "nint", "nuint" };
            foreach (string keyword in keywordTypes)
                s_registry[$"{keyword}[]"] = s_registry[keyword].MakeArrayType();
        }

        public static bool Contains(string name) => s_registry.ContainsKey(name);
        public static bool TryGetType(string name, out Type? cachedtype) => s_registry.TryGetValue(name, out cachedtype);

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
            if (type.IsDefined(typeof(CompilerGeneratedAttribute)) || IsFileType())
                return false;
            ThrowIfBadKey(key);
            if (CheckDuplicateOrCached())
                return false;
            s_registry.TryAdd(key, type);
            return true;

            bool IsFileType()
            {
                if (!type.IsPublic && !type.IsNested)
                    return type.Name.StartsWith("<");
                return false;
            }

            bool CheckDuplicateOrCached()
            {
                if (TryGetType(key, out Type? cachedtype))
                    return type == cachedtype ? true : throw new DuplicateKeyException(cachedtype!, type, key);
                return false;
            }
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


        public static void CacheTypes(IEnumerable<Type> types, bool fullname)
        {
            foreach (Type type in types)
                CacheType(type, fullname);

        }

        public static void CacheTypes(IEnumerable<Type> types, Func<Type, string?> toString)
        {
            foreach (Type type in types)
            {
                string? key = toString.Invoke(type);
                if (key != null)
                    CacheType(key, type);
            }
        }

        // public static void CacheTypes(IEnumerable<KeyValuePair<string, Type>> book)
        // {
        //     foreach (var pair in book)
        //         CacheType(pair.Key, pair.Value);
        // }
        public static string GetCompatibleName(Type type, bool fullname)
        {
            string name = fullname ? type.FullName ?? throw new ArgumentNullException(nameof(fullname), $"Type {type} returned null for fullname.") : type.Name;
            if (type.IsGenericTypeDefinition)
                return GenericToString();
            else if (type.IsNested && fullname)
                return name.Replace('+', '.');
            else
                return name;

            string GenericToString()
            {
                StringBuilder snippedname = SnipGenericName();
                if (fullname && type.IsNested)
                    snippedname.Replace('+', '.');
                snippedname.Append('T');
                int genericargs = type.GetGenericArguments().Length;
                if (genericargs > 0)
                    snippedname.Append(genericargs);
                return snippedname.ToString();

                StringBuilder SnipGenericName()
                {
                    StringBuilder sb = new();
                    foreach (char c in name)
                        if (c == '`')
                            break;
                        else sb.Append(c);
                    return sb;
                }
            }

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
                {
                    if (!MethodLexer.ValidIdentifierFirstChar(value))
                        throw new ArgumentException($"Member access must be followed by an underscore or a letter for namespaces. Bad Key: {key}");
                    accessor = false;
                }

            }

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
            public static IEnumerable<string> Fields(Type t, string? contains = null, BindingFlags search = Navigator.Flag)
            {
                Dictionary<string, FieldInfo> fields = new();
                Navigator.MapType(null, null, fields, t);
                return ReadMembers(fields, contains, search);
            }
            public static IEnumerable<string> Overloads(Type t, string? contains = null, BindingFlags search = Navigator.Flag)
            {
                Dictionary<Navigator.ResolvedOverload, MethodBase> overloads = new();
                Navigator.MapType(null, overloads, null, t, contains?.EqualsCaseless("new") ?? true);
                var asStrings = overloads.Select(x => new KeyValuePair<string, MethodBase>(x.Key.ToString(), x.Value));
                return ReadMembers(asStrings, contains, search);
            }


            public static IEnumerable<string> Methods(Type t, string? contains = null, BindingFlags search = Navigator.Flag)
            {
                Dictionary<string, MethodBase> methods = new();
                Navigator.MapType(methods, null, null, t, contains?.EqualsCaseless("new") ?? true);
                return ReadMembers(methods, contains, search);
            }


            public static void FlushStaticCache(bool ambiguousMatches, bool accessedMembers, bool reifiedGenerics)
            {
                Navigator.FlushStaticCache(ambiguousMatches, accessedMembers, reifiedGenerics);
            }
            public static Type Of<T>() => typeof(T);
            public static Type Of(string x) => GetTypeOrThrow(x); //for generic definitions

            public static T? New<T>(T? obj = default) => obj; ///helpful for instantiating enums with OR
            public static T New<T>() where T : new() => new();
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

            static IEnumerable<string> ReadMembers<T>(IEnumerable<KeyValuePair<string, T>> members, string? key, BindingFlags flags) where T : MemberInfo
            {
                if (key != null)
                {
                    bool contained = false;
                    foreach (var member in members)
                    {
                        if (SearchModifiers.ProcessSearch(member.Value, flags) && member.Key.Contains(key, StringComparison.OrdinalIgnoreCase))
                        {
                            contained = true;
                            yield return $"[Key: {member.Key} :: {ReflectionPrinter.Print(member.Value)}]";
                        }
                    }
                    if (!contained)
                        yield return $"No {typeof(T).Name} found with name containing {key} with search option {flags}.";
                }
                else
                    foreach (var member in members)
                        if (SearchModifiers.ProcessSearch(member.Value, flags))
                            yield return $"[Key: {member.Key} :: {ReflectionPrinter.Print(member.Value)}]";
            }



            struct SearchModifiers
            {
                bool _static;
                bool _public;
                bool _inherited;

                public static bool ProcessSearch(MemberInfo inf, BindingFlags flags)
                {
                    return inf is MethodBase mthd ? New(mthd).ProcessSearch(flags) : New((FieldInfo)inf).ProcessSearch(flags);
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

                public static SearchModifiers New(MethodBase method)
                {
                    SearchModifiers modifiers = new()
                    {
                        _static = method.IsStatic,
                        _public = method.IsPublic
                    };
                    if (method.DeclaringType != null && method is MethodInfo mthinfo)
                        modifiers._inherited = method.ReflectedType != mthinfo.DeclaringType;
                    return modifiers;
                }
                public static SearchModifiers New(FieldInfo field)
                {
                    SearchModifiers extract = new()
                    {
                        _static = field.IsStatic,
                        _public = field.IsPublic
                    };
                    if (field.DeclaringType != null)
                        extract._inherited = field.DeclaringType != field.ReflectedType;
                    return extract;
                }
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