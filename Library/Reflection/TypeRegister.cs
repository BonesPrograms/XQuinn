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
    public static partial class TypeRegister
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

        internal static readonly Dictionary<GenericKey, Type> s_registry = new();

        readonly static string[] s_illegal_keys = new string[] { "null", "default", "base", "this", bool.FalseString, bool.TrueString }; ///Reserved "language" keywords

        static TypeRegister()
        {
            (string, Type)[] preNamedTypes = new (string, Type)[]
            {
              ("int", typeof(int)),  ("uint", typeof(uint)),
              ("short", typeof(short)), ("ushort", typeof(ushort)),
              ("long", typeof(long)), ("ulong", typeof(ulong)),
              ("float", typeof(float)), ("Tuple", typeof(ValueTuple)),
              ("KVP", typeof(KeyValuePair<,>))
            };
            for (int i = 0; i < preNamedTypes.Length; i++)
            {
                (string name, Type type) = preNamedTypes[i];
                GenericKey gkey = new(name, type);
                s_registry[gkey] = type;
            }
            Type[] preCachedTypes = new Type[] //Most types are cached with a name based on their type object's name property
            {                                   //so its easier to just add new ones to the array
            typeof(object), typeof(string),
            typeof(sbyte),  typeof(byte),
            typeof(bool), typeof(char),
            typeof(double), typeof(decimal),

            typeof(Enum), typeof(BindingFlags),
            typeof(Nullable<>), typeof(IDisposable),

            typeof(Types), typeof(TypeRegister),

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
                s_registry[gkey] = type;
            }
            Assembly mscorlib = Assembly.Load("System.Private.CoreLib");
            Type runtimeType = mscorlib.GetType("System.RuntimeType", true)!;
            GenericKey key = new(typeof(Type).Name, runtimeType);
            s_registry[key] = runtimeType; //There is a desync between Type's resolved method overloads and RuntimeType's resolved method overloads.
        }                                               //Type becomes RuntimeType at runtime, so it is irrelevent to us, we cache RuntimeType instead.

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
            throw new ArgumentException($"Could not find cached type with key {key.Name} and generic arg count {key.Args}.");
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
            s_registry[trueKey] = type;
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
            if (char.IsDigit(key[0]))
                throw new ArgumentException($"Keys cannot begin with a digit. Bad Key: {key}");
            if (key[0] == CallLexer.MemberAccessOrDecimal)
                throw new ArgumentException($"Keys cannot begin with a period. Bad Key {key}");
            for (int i = 0; i < s_illegal_keys.Length; i++)
                if (s_illegal_keys[i].EqualsCaseless(key))
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