using System.Collections.ObjectModel;
using System.Reflection;
using System;
using System.Collections.Generic;
using XQuinn.LangInterp;
using XQuinn.Extensions;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;

using System.Runtime.CompilerServices;
using System.Linq;
using XQuinn.Runtime.NavigatorEngine;
using XQuinn.Runtime;
using XQuinn.ObjectModel;

namespace XQuinn.Reflection
{
    public static partial class TypeRegister
    {
        public static IEnumerable<string> Keys()
        {
            foreach (GenericKey key in Registry.Keys)
                yield return key.ToString();
        }
        public static ICollection<Type> Values => Registry.Values;
        public static IEnumerable<KeyValuePair<string, Type>> Enumerate()
        {
            foreach (KeyValuePair<GenericKey, Type> obj in Registry)
                yield return new(obj.Key.ToString(), obj.Value);
        }

        static Dictionary<GenericKey, Type> Registry { get; } = new();
        static string[] IllegalKeys { get; } = new string[] { "null", "default", "base", "this", bool.FalseString, bool.TrueString };
        static TypeRegister()
        {
            (string, Type)[] preNamedTypes = new (string, Type)[]
            {
              ("int", typeof(int)),  ("uint", typeof(uint)),
              ("short", typeof(short)), ("ushort", typeof(ushort)),
              ("long", typeof(long)), ("ulong", typeof(ulong)),
              ("float", typeof(float)), ("bool", typeof(bool)),
              ("Tuple", typeof(ValueTuple)), ("KVP", typeof(KeyValuePair<,>))
            };
            for (int i = 0; i < preNamedTypes.Length; i++)
            {
                (string name, Type type) = preNamedTypes[i];
                GenericKey gkey = new(name, type);
                Registry[gkey] = type;
            }
            Type[] preCachedTypes = new Type[]
            {
            typeof(object), typeof(string),
            typeof(sbyte),  typeof(byte),
            typeof(char), typeof(double),
            typeof(decimal),

            typeof(Enum), typeof(BindingFlags),
            typeof(Nullable<>), typeof(Nullable),
            typeof(IDisposable),

            typeof(Types), typeof(TypeRegister), typeof(InstanceReader),

            typeof(Assembly), typeof(Activator),
            typeof(Environment), typeof(AppDomain),
            typeof(AppContext), typeof(RuntimeEnvironment),
            typeof(RuntimeInformation),

            typeof(Array), typeof(List<>), typeof(IList<>), typeof(IList),
            typeof(Enumerable), typeof(IEnumerable), typeof(IEnumerable<>),
            typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IDictionary),
            typeof(HashSet<>), typeof(ICollection<>), typeof(ICollection), typeof(Collection<>)
            };
            for (int i = 0; i < preCachedTypes.Length; i++)
            {
                Type type = preCachedTypes[i];
                string name = GetCompatibleName(type, false);
                GenericKey gkey = new(name, type);
                Registry[gkey] = type;
            }
            Assembly mscorlib = Assembly.Load("System.Private.CoreLib");
            Type runtimeType = mscorlib.GetType("System.RuntimeType", true)!;
            GenericKey key = new(typeof(Type).Name, runtimeType);
            Registry[key] = runtimeType;
        }

        public static bool Contains(string name) => Registry.ContainsKey(GenericKey.TypeQuery(name));
        public static bool TryGetType(string name, out Type? cachedtype) => Registry.TryGetValue(GenericKey.TypeQuery(name), out cachedtype);
        public static Type? GetTypeCached(string name)
        {
            if (TryGetType(name, out Type? cachedtype))
                return cachedtype;
            return null;
        }
        public static Type GetTypeOrThrow(string name)
        {
            GenericKey key = GenericKey.TypeQuery(name);
            return GetTypeOrThrow(key);
        }

        internal static Type GetTypeOrThrow(GenericKey key)
        {
            if (Registry.TryGetValue(key, out Type? cachedType))
                return cachedType;
            throw new ArgumentException($"Could not find cached type with key {key.Name} and generic arg count {key.Args}.");
        }

        public static bool CacheType(Type type, string key)
        {
            if (type.IsNested)
                return false;
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), true) || TypeBook.IsFileType(type))
                return false;
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                throw new NotSupportedException($"Generic type {type} with key {key} cannot be cached. Only generic type definitions and nongeneric types can be cached.");
            ThrowIfBadKey(key);
            GenericKey trueKey = new(key, type);
            if (CheckDuplicateOrCached(type, trueKey))
                return false;
            Registry[trueKey] = type;
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
        public static List<(string key, Type type)>? CacheTypesSkipDuplicates(IEnumerable<Type> types, bool fullname)
        {
            List<(string, Type)>? dic = null;
            foreach (Type type in types)
            {
                try
                {
                    CacheType(type, fullname);
                }
                catch (DuplicateKeyException ex)
                {
                    dic ??= new();
                    dic.Add((ex.GKey.ToString(), type));
                }
            }
            return dic;
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
            if (char.IsDigit(key[0]))
                throw new ArgumentException($"Keys cannot begin with a digit. Bad Key: {key}");
            if (key[0] == CallLexer.MemberAccessOrDecimal)
                throw new ArgumentException($"Keys cannot begin with a period. Bad Key {key}");
            for (int i = 0; i < IllegalKeys.Length; i++)
                if (IllegalKeys[i].EqualsCaseless(key))
                    throw new ArgumentException($"This key is restricted and cannot be registered. Bad Key {key}.");
            bool accessor = false;
            for (int i = 0; i < key.Length; i++)
            {
                char value = key[i];
                if (CallLexer.Illegal(value))
                {
                    if (!accessor && value == CallLexer.MemberAccessOrDecimal)
                        accessor = true;
                    else
                        throw new ArgumentException($"Keys can only consist of digits, letters, underscores, or single periods between names. Bad Key {key}.");
                }
                else if (accessor)
                {
                    if (!CallLexer.ValidIdentifierFirstChar(value))
                        throw new ArgumentException($"Member access must be followed by an underscore or a letter for namespaces. Bad Key: {key}");
                    accessor = false;
                }
            }
        }

        static bool CheckDuplicateOrCached(Type type, GenericKey key)
        {
            if (Registry.TryGetValue(key, out Type? cachedtype))
                return type == cachedtype ? true : throw new DuplicateKeyException(cachedtype!, type, key);
            return false;
        }

    }

    internal class DuplicateKeyException : Exception
    {
        public readonly GenericKey GKey;
        internal DuplicateKeyException(Type type, Type insert, GenericKey key) : this(type, insert, key.ToString())
        {
            GKey = key;
        }
        DuplicateKeyException(Type type, Type insert, string name) : base($"Conflict detected when trying to cache {insert.FullName} with key {name}. Key has already been used for type {type.FullName}. Key names are not case sensitive.")
        {
        }
    }
}
