using XQuinn.Runtime;
using XQuinn.Reflection;
using System.Collections.Immutable;
using XQuinn.IO;

//namespace XQuinn.CorLib;
namespace XQuinn.CorLib;

public static class Core
{
    // public static bool DefaultWhere(Type t) => t.Namespace !=
    // public static readonly ImmutableArray<string> BadNames = ["Object", "Method", "Element", "Field", "Parameter", "TypeString", "Token", "Core"];
    public static readonly IReadOnlySet<string> ChangedTypeNames;
    static readonly HashSet<string> _changedNames;
    static Core()
    {
        _changedNames = [];
        ChangedTypeNames = _changedNames.AsReadOnly();
    }

    public static void PrintChangedTypeNames(string path)
    {
        Write.SafetyCheck(path);
        using StreamWriter writer = new(path);
        foreach (string name in ChangedTypeNames)
        {
            writer.WriteLine(name);
        }
    }
    public static string? DefaultToString(Type t)
    {
        if (t == typeof(Parsing.AST.Field))
            return "AST.Field";
        else if (t == typeof(Reflection.Tokenizer.Field))
            return "Tokenizer.Field";
        else if (TypeCache.TryGetType(t.Name, out Type? cached))
        {
            if (cached == t)
                return null;
            else
            {
                string name = $"xq_{t.Name}";
                _changedNames.Add(name);
                return name;
            }
        }
        if (t.IsPublic && !t.IsNested)
            return t.Name;
        else
            return null;
    }

    // public static bool DefaultWhere(Type t)
    // {
    //     if (TypeCache.TryGetValue(t.Name, out Type? cached) && cached == t) //skip already cached xquinn types (if that ever happens)
    //         return false;
    //     return t.IsPublic && !t.IsNested;
    // }

    public static void InitializeXQuinn(bool fullname = false, Func<Type, string?>? toString = null)
    {
        TypeBook book = CacheXQuinn(fullname, toString);
        RuntimeCommand.Register(book.Types, typeof(Core).Module.Assembly.GetName().FullName);
    }
    public static TypeBook CacheXQuinn(bool fullname = false, Func<Type, string?>? toString = null)
    {
        TypeBook book = TypeBook.New(typeof(Core).Module.GetTypes(), fullname, toString);
        TypeCache.CacheType(book);
        return book;
    }

}