using System.Reflection;
using XQuinn.Reflection;
using XQuinn.Extensions;
using System.Collections.Immutable;
using static XQuinn.Reflection.MemberGroup;
using System.Collections.Concurrent;
using Mono.Reflection;
namespace XQuinn.Reflection;




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
    readonly ConcurrentDictionary<string, Type> _book;
    public readonly IReadOnlyDictionary<string, Type> Book;
    public int Count => _book.Count;
    public ICollection<Type> Types => _book.Values;
    public ICollection<string> Names => _book.Keys;
    public IEqualityComparer<string> StringComparer => _book.Comparer;

    /// <summary>
    /// True = fullname, false == short name, null == assortment of both
    //
    /// <summary>
    /// It is recommended to use a string comparer if building on your own. I use OrdinalIgnoreCase.
    /// </summary>   
    TypeBook(ConcurrentDictionary<string, Type> book)
    {
        _book = book;
        Book = book.AsReadOnly();
    }

    public Type this[string key]
    {
        get => _book[key];
      //  set => _book[key] = value;
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _book.GetEnumerator();
    public IEnumerator<KeyValuePair<string, Type>> GetEnumerator() => _book.GetEnumerator();
    public bool TryGetValue(string key, out Type? value) => _book.TryGetValue(key, out value);
   // public bool TryAdd(string key, Type value) => _book.TryAdd(key, value);
    public bool ContainsKey(string key) => _book.ContainsKey(key);

    //ToString here is for custom filtering, ie. maybe one of your shortnames are already taken, you can have your filter pre-check if one of your types are already cached
    //and then return a different name, you can also return null and it will *skip* adding that type to the typebook. Using a ToString will also completely override
    //the default procedure for choosing keys.
    public static TypeBook New(IEnumerable<Type> types, bool fullname, Func<Type, string?>? toString = null, StringComparer? comp = null)
    {
        comp ??= System.StringComparer.OrdinalIgnoreCase;
        ConcurrentDictionary<string, Type> book = new(comp);
        foreach (var type in types)
        {
            if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            {
                if (!type.IsPublic && !type.IsNested)
                {
                    if (type.Name.Contains("__"))
                        continue;
                }
                string? txt = null;
                if (toString != null)
                {
                    txt = toString(type);
                    if (txt == null)
                        continue;
                }
                else
                {
                    if (fullname)
                    {
                        if (type.FullName == null)
                            throw new InvalidOperationException($"Fullname for type {type.Name} does not exist.");
                        txt = type.FullName;
                    }
                    else
                        txt = type.Name;
                }
                if (book.TryGetValue(txt, out var val))
                    throw new DuplicateTypeNameException(val, type, txt);
                book.TryAdd(txt, type);
            }
        }
        return new(book);
    }


}
