using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System;
using System.Collections.Generic;
using XQuinn.CodeAnalysis;
using XQuinn.Extensions;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;
using XQuinn.ObjectModel;
using System.Runtime.CompilerServices;
using System.Linq;
using XQuinn.Runtime.NavigatorEngine;
using System.ComponentModel;


namespace XQuinn.Reflection
{
    public static partial class TypeCache
    {

        public static IEnumerable<string> Keys()
        {
            foreach (GenericKey key in s_registry.Keys)
                yield return key.ToString();
        }
        public static ICollection<Type> Values => s_registry.Values;
        public static IEnumerable<KeyValuePair<string, Type>> Enumerate()
        {
            foreach (KeyValuePair<GenericKey, Type> obj in s_registry)
                yield return new(obj.Key.ToString(), obj.Value);
        }
        internal static readonly ConcurrentDictionary<GenericKey, Type> s_registry = new()
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
            [new(nameof(Nullable<_>), 1)] = typeof(Nullable<>),
            [new(nameof(IDisposable))] = typeof(IDisposable),

            [new(nameof(Types))] = typeof(Types),
            [new(nameof(InstanceReader))] = typeof(InstanceReader),
            [new(nameof(ILReader))] = typeof(ILReader),
            [new(nameof(TypeCache))] = typeof(TypeCache),
            [new(nameof(Assembly))] = typeof(Assembly),
            [new(nameof(Activator))] = typeof(Activator),
            [new(nameof(Convert))] = typeof(Convert),
            [new(nameof(TypeConverter))] = typeof(TypeConverter),

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
            [new(nameof(IEnumerable), 1)] = typeof(IEnumerable<>),
            [new(nameof(Dictionary<_, _>), 2)] = typeof(Dictionary<,>),
            [new(nameof(IDictionary), 2)] = typeof(IDictionary<,>),
            [new(nameof(IDictionary))] = typeof(IDictionary),
            [new("KVP", 2)] = typeof(KeyValuePair<,>),
            [new(nameof(HashSet<_>), 1)] = typeof(HashSet<>),
            [new(nameof(Collection<_>), 1)] = typeof(Collection<>),
            [new(nameof(ICollection))] = typeof(ICollection),
            [new(nameof(ICollection), 1)] = typeof(ICollection<>)

        };

        readonly static string[] _illegalKeys = new string[] { "null", "default", "base", "this" }; ///Reserved "language" keywords

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

        public static bool Contains(string name) => s_registry.ContainsKey(GenericKey.TypeQuery(name));
        public static bool TryGetType(string name, out Type? cachedtype) => s_registry.TryGetValue(GenericKey.TypeQuery(name), out cachedtype);
        public static Type? GetTypeCached(string name)
        {
            if (TryGetType(name, out Type? cachedtype))
                return cachedtype;
            return null;
        }
        public static Type GetTypeOrThrow(string name)//ReadOnlyDictionary<string, Type>? book = null)
        {
            GenericKey key = GenericKey.TypeQuery(name);
            return GetTypeOrThrow(key);
        }

        internal static Type GetTypeOrThrow(GenericKey key)
        {
            if (s_registry.TryGetValue(key, out Type? cachedType))
                return cachedType;
            throw new ArgumentException($"Could not find cached type with key {key.Key} and generic arg count {key.Args}.");
        }


        public static bool CacheType(Type type, string key)
        {
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), true) || TypeBook.IsFileType(type))
                return false;
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                throw new NotSupportedException($"Generic type {type} with key {key} cannot be cached. Only generic type definitions and nongeneric types can be cached.");
            ThrowIfBadKey(key);
            GenericKey trueKey = new(key, type);
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

        public static void CacheTypes(IEnumerable<Type> types, bool fullname, string append)
        {
            foreach (Type type in types)
            {
                string name = GetCompatibleName(type, fullname);
                CacheType(type, $"{append}{name}");
            }
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
        internal static void ThrowIfBadKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Empty key.");
            if (key[0].IsDigit())
                throw new ArgumentException($"Keys cannot begin with a digit. Bad Key: {key}");
            if (key[0] == '.')
                throw new ArgumentException($"Keys cannot begin with a period. Bad Key {key}");
            for (int i = 0; i < _illegalKeys.Length; i++)
                if (_illegalKeys[i].EqualsCaseless(key))
                    throw new ArgumentException($"This key is restricted and cannot be registered. Bad Key {key}.");
            bool accessor = false;
            for (int i = 0; i < key.Length; i++)
            {
                char value = key[i];
                if (!value.IsDigit() && !value.IsLetter() && value != '_')
                {
                    if (!accessor && value == '.')
                        accessor = true;
                    else
                        throw new ArgumentException($"Keys can only consist of digits, letters, underscores, or single periods between names. Bad Key {key}.");
                }
                else if (accessor)
                {
                    if (!InvokeLexer.ValidIdentifierFirstChar(value))
                        throw new ArgumentException($"Member access must be followed by an underscore or a letter for namespaces. Bad Key: {key}");
                    accessor = false;
                }

            }

        }
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

        static bool CheckDuplicateOrCached(Type type, GenericKey key)
        {
            if (s_registry.TryGetValue(key, out Type? cachedtype))
                return type == cachedtype ? true : throw new DuplicateKeyException(cachedtype!, type, key);
            return false;
        }
        struct _
        {

        }
    }

    internal class DuplicateKeyException : Exception
    {
        internal DuplicateKeyException(Type type, Type insert, GenericKey name) : this(type, insert, name.ToString())
        {

        }
        internal DuplicateKeyException(Type type, Type insert, string name) : base($"Conflict detected when trying to cache {insert.FullName} with key {name}. Key has already been used for type {type.FullName}. Key names are not case sensitive.")
        {

        }
    }
}