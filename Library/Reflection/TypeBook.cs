using System.Reflection;
using XQuinn.Reflection;
using XQuinn.Extensions;
using static XQuinn.Reflection.MemberGroup;
using System.Collections.Concurrent;
using Mono.Reflection;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
namespace XQuinn.Reflection
{




    // 
    // Pro Tip: Shortnames on generic definitions will come out followed by a ` and the number of generic arguments.
    // IE. A generic definition named Generic<T> will have the short name Generic`1.
    // //It is recommended not to override this because there can be multiple generic definitions with the same name but a different number of generic parameter

    /// <summary>
    /// /// A case-insensitive readonly wrapper for a dictionary of type names for quick string lookup and caching. This isnt much more special than a dictionary, the only 
    /// difference is it simplifies creation.
    /// </summary>
    public sealed class TypeBook : IEnumerable<KeyValuePair<string, Type>>
    {
        //readonly ConcurrentDictionary<string, Type> _book;
        public readonly IReadOnlyDictionary<string, Type> Book;
        public int Count => Book.Count;
        public IEnumerable<Type> Types => Book.Values;
        public IEnumerable<string> Names => Book.Keys;

        /// <summary>
        /// True = fullname, false == short name, null == assortment of both
        //
        /// <summary>
        /// It is recommended to use a string comparer if building on your own. I use OrdinalIgnoreCase.
        /// </summary>   
        TypeBook(ConcurrentDictionary<string, Type> book)
        {
            //_book = book;
            Book = new ReadOnlyDictionary<string, Type>(book);
        }

        public Type this[string key]
        {
            get => Book[key];
            // set => _book[key] = value;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => Book.GetEnumerator();
        public IEnumerator<KeyValuePair<string, Type>> GetEnumerator() => Book.GetEnumerator();
        public bool TryGetValue(string key, out Type? value) => Book.TryGetValue(key, out value);
        // public bool TryAdd(string key, Type value) => _book.TryAdd(key, value);
        public bool ContainsKey(string key) => Book.ContainsKey(key);

        //ToString here is for custom filtering, ie. maybe one of your shortnames are already taken, you can have your filter pre-check if one of your types are already cached
        //and then return a different name, you can also return null and it will *skip* adding that type to the typebook. Using a ToString will also completely override
        //the default procedure for choosing keys.

        public static TypeBook New(IEnumerable<Type> types, Func<Type, string?> toString, StringComparer? comp = null, bool excludeFileScoped = true)
        {
            ConcurrentDictionary<string, Type> book = new(comp);
            foreach (Type type in types)
            {
                if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
                {
                    if (excludeFileScoped && IsFileType(type)) continue;
                    string? key = toString(type);
                    if (key == null) continue;
                    if (book.TryGetValue(key, out Type? cached)) throw new DuplicateKeyException(cached, type, key);
                    book.TryAdd(key, type);
                }
            }
            return new(book);
        }

        static bool IsFileType(Type t)
        {
            if (!t.IsPublic && !t.IsNested) return t.Name.StartsWith("<");
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
        public static TypeBook New(IEnumerable<Type> types, bool fullname, StringComparer? comp = null, bool excludeFileScoped = true)
        {
            ConcurrentDictionary<string, Type> book = new(comp);
            foreach (Type type in types)
            {
                if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
                {
                    if (excludeFileScoped && IsFileType(type)) continue;
                    string key = fullname == false ? type.Name : type.FullName ?? throw new ArgumentException() ;
                    if (book.TryGetValue(key, out Type? cached)) throw new DuplicateKeyException(cached, type, key);
                    book.TryAdd(key, type);
                }
            }
            return new(book);
        }


    }
}