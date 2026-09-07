using System.Reflection;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using XQuinn.CodeAnalysis.AST;
using XQuinn.Private.EqualityHelpers;

namespace XQuinn.Reflection
{

    internal readonly struct TypeKey : IEquatable<TypeKey>, IIndexKeyPair
    {
        public string Key => _name;
        public int Index => _argCount;
        readonly string _name;
        readonly int _argCount;
        public static TypeKey Query(string typename)
        {
            if (GenericString.HasTypeArgs(typename))
            {
                TypeString query = TypeString.NewGeneric(typename);
                TypeKey key = new(query);
                return key;
            }
            return new(typename);
        }

        //  public static TypeKey Generate(Type t) => new(GetCompatibleName(t, false), t);

        public TypeKey(string key, int args = 0)
        {
            _name = key;
            _argCount = args;
        }

        public TypeKey(string key, Type t) : this(key, t.IsGenericTypeDefinition ? t.GetGenericArguments().Length : 0)
        {
        }


        internal TypeKey(TypeString str) : this(str.NameOrValue, str.Generics.Count)
        {
        }

        bool IEquatable<TypeKey>.Equals(TypeKey other)
        {
            return Equals(other);
        }

        public override bool Equals(object? obj)
        {
            return obj is TypeKey key && Equals(key);
        }

        public bool Equals(TypeKey key)
        {
            return IndexKeyPair<TypeKey>.Equals(this, key);
        }

        public override int GetHashCode()
        {
            return IndexKeyPair<TypeKey>.HashCode(23, this);
        }

        public static bool operator ==(TypeKey left, TypeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TypeKey left, TypeKey right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            if (_argCount > 0)
            {
                if (_argCount == 1)
                    return $"{_name}<T>";
                StringBuilder sb = new(_name);
                sb.Append("<T1, ");
                for (int i = 1; i < _argCount; i++)
                {
                    sb.Append($"T{i + 1}");
                    if (For.NeedsDelimiter(_argCount, i))
                    {
                        sb.Append(", ");
                    }
                }
                sb.Append('>');
                return sb.ToString();
            }
            return _name;
        }

    }


    // 
    // Pro Tip: Shortnames on generic definitions will come out followed by a ` and the number of generic arguments.
    // IE. A generic definition named Generic<T> will have the short name Generic`1.
    // //It is recommended not to override this because there can be multiple generic definitions with the same name but a different number of generic parameter

    /// <summary>
    /// /// A case-insensitive readonly wrapper for a dictionary of type names for quick string lookup and caching. This isnt much more special than a dictionary, the only 
    /// difference is it simplifies creation.
    /// </summary>
    internal sealed class TypeBook : IReadOnlyDictionary<TypeKey, Type>
    {
        //readonly ConcurrentDictionary<string, Type> _book;
        readonly Dictionary<TypeKey, Type> _book;
        public int Count => _book.Count;
        public IEnumerable<TypeKey> Keys => _book.Keys;
        public IEnumerable<Type> Values => _book.Values;

        public Type this[TypeKey key] => _book[key];
        TypeBook(Dictionary<TypeKey, Type> book)
        {
            //_book = book;
            _book = book;
        }
        IEnumerator IEnumerable.GetEnumerator() => _book.GetEnumerator();
        public IEnumerator<KeyValuePair<TypeKey, Type>> GetEnumerator() => _book.GetEnumerator();

#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        public bool TryGetValue(TypeKey key, out Type? value) => _book.TryGetValue(key, out value);
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        // public bool TryAdd(string key, Type value) => _book.TryAdd(key, value);
        public bool ContainsKey(TypeKey key) => _book.ContainsKey(key);

        //ToString here is for custom filtering, ie. maybe one of your shortnames are already taken, you can have your filter pre-check if one of your types are already cached
        //and then return a different name, you can also return null and it will *skip* adding that type to the typebook. Using a ToString will also completely override
        //the default procedure for choosing keys.

        public static TypeBook New(IEnumerable<Type> types, Func<Type, string?> toString)
        {
            Dictionary<TypeKey, Type> book = new();
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
                    TypeKey realKey = new(key, type);
                    if (book.TryGetValue(realKey, out Type? cached))
                        throw new DuplicateKeyException(cached, type, key);
                    book[realKey] = type;
                }
            }
            return new(book);
        }

        // static bool CompilerGenerated(Type? type)
        // {
        //     while (type != typeof(object) && type != null)
        //     {
        //         if (type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))) return true;
        //         else return false;
        //     }
        //     return false;
        // }
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

        //         if (type == null) throw new ArgumentNullException(nameof(type));

        //         // Look for the [CompilerFeatureRequired] attribute assigned by the compiler
        //         var attribute = type.GetCustomAttributes(typeof(CompilerFeatureRequiredAttribute), inherit: false)
        //                             .FirstOrDefault() as CompilerFeatureRequiredAttribute;

        //         // Check if the required feature matches "FileLocalTypes"
        //         return attribute != null && attribute.FeatureName == "FileLocalTypes";
        //     }
        // }
        public static TypeBook New(IEnumerable<Type> types, bool fullname, bool excludeFileScoped = true)
        {
            Dictionary<TypeKey, Type> book = new();
            foreach (Type type in types)
            {
                if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
                {
                    if (excludeFileScoped && IsFileType(type))
                        continue;
                    string key = fullname == false ? type.Name : type.FullName ?? throw new ArgumentNullException();
                    TypeKey realKey = new(key, type);
                    if (book.TryGetValue(realKey, out Type? cached))
                        throw new DuplicateKeyException(cached, type, key);
                    book[realKey] = type;
                }
            }
            return new(book);
        }


    }
}