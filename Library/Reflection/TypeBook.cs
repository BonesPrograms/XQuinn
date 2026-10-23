using System.Reflection;
using System;
using System.Collections.Generic;
using System.Collections;
using System.CodeDom.Compiler;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.Reflection
{
    internal sealed class TypeBook : IReadOnlyDictionary<GenericKey, Type>
    {
        readonly Dictionary<GenericKey, Type> _book;
        public int Count => _book.Count;
        public IEnumerable<GenericKey> Keys => _book.Keys;
        public IEnumerable<Type> Values => _book.Values;

        public Type this[GenericKey key] => _book[key];
        TypeBook(Dictionary<GenericKey, Type> book)
        {
            _book = book;
        }
        IEnumerator IEnumerable.GetEnumerator() => _book.GetEnumerator();
        public IEnumerator<KeyValuePair<GenericKey, Type>> GetEnumerator() => _book.GetEnumerator();

#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        public bool TryGetValue(GenericKey key, out Type? value) => _book.TryGetValue(key, out value);
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).

        public bool ContainsKey(GenericKey key) => _book.ContainsKey(key);

        public static TypeBook New(IEnumerable<Type> types, Func<Type, string?> toString)
        {
            Dictionary<GenericKey, Type> book = new();
            foreach (Type type in types)
            {
                if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))
#if NET7_0_OR_GREATER
                     && !IsGeneratedRegexType(type)
#endif
                )
                {
                    if (IsFileType(type))
                        continue;
                    string? key = toString(type);
                    if (key == null)
                        continue;
                    GenericKey realKey = new(key, type);
                    if (book.TryGetValue(realKey, out Type? cached))
                        throw new DuplicateKeyException(cached, type, realKey);
                    book[realKey] = type;
                }
            }
            return new(book);
        }

#if NET7_0_OR_GREATER
        internal static bool IsGeneratedRegexType(Type type)
        {
            for (var current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsDefined(typeof(GeneratedCodeAttribute), inherit: false))
                {
                    var attribute = current.GetCustomAttribute<GeneratedCodeAttribute>();

                    if (attribute?.Tool == "System.Text.RegularExpressions.Generator")
                        return true;
                }
            }

            return false;
        }
#endif
        internal static bool IsFileType(Type t)
        {
            if (!t.IsPublic && !t.IsNested)
                return t.Name.StartsWith("<");
            else return false;
        }

        public static TypeBook New(IEnumerable<Type> types, bool fullname, bool excludeFileScoped = true)
        {
            Dictionary<GenericKey, Type> book = new();
            foreach (Type type in types)
            {
                if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
                {
                    if (excludeFileScoped && IsFileType(type))
                        continue;
                    string key = fullname == false ? type.Name : type.FullName ?? throw new ArgumentNullException();
                    GenericKey realKey = new(key, type);
                    if (book.TryGetValue(realKey, out Type? cached))
                        throw new DuplicateKeyException(cached, type, realKey);
                    book[realKey] = type;
                }
            }
            return new(book);
        }
    }
}
