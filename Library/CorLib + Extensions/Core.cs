using XQuinn.Runtime;
using XQuinn.Reflection;
using XQuinn.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.IO;
using System.Linq;
#if NET6_0_OR_GREATER
using XQuinn.Collections;
#endif

//namespace XQuinn.CorLib;
namespace XQuinn.CorLib
{




    public static class Core
    {
        // public static bool DefaultWhere(Type t) => t.Namespace !=
        // public static readonly ImmutableArray<string> BadNames = ["Object", "Method", "Element", "Field", "Parameter", "TypeString", "Token", "Core"];

#if NET6_0_OR_GREATER
        public static IReadOnlySet<string>? ChangedTypeNames => _namesreadonly;
        static IReadOnlySet<string>? _namesreadonly;

        static HashSet<string>? _changedNames;

        public static void PrintChangedTypeNames(string path)
        {
            if (_namesreadonly != null)
            {
                Write.SafetyCheck(path);
                using StreamWriter writer = new(path);

                foreach (string name in _namesreadonly)
                {
                    writer.WriteLine(name);
                }
            }
        }
#endif
        public static string? DefaultToString(Type t)
        {
            if (t == typeof(Parsing.AST.Field))
                return "AST.Field";
            else if (t == typeof(ObjectModel.Tokenizer.Field))
                return "Tokenizer.Field";//internal shortname assembly conflict
            else if (t == typeof(Core))
                return null;
            else if (TypeCache.TryGetType(t.Name, out Type? cached))
            {
                if (cached == t)
                    return null;
                else
                {
#if NET6_0_OR_GREATER
                    _changedNames ??= new(StringComparer.OrdinalIgnoreCase);
                    _namesreadonly ??= new Net6ReadOnlySet<string>(_changedNames);
                    _changedNames.Add(t.Name);
#endif
                    return $"xq_{t.Name}";
                }
            }
            if (t.IsPublic) //nested is automatically excluded here 
            {
                if (t.IsGenericTypeDefinition)
                {
                    return t.Name.Remove(t.Name.IndexOf('`'));
                }
                return t.Name;
            }
            else
                return null;
        }

        // public static bool DefaultWhere(Type t)
        // {
        //     if (TypeCache.TryGetValue(t.Name, out Type? cached) && cached == t) //skip already cached xquinn types (if that ever happens)
        //         return false;
        //     return t.IsPublic && !t.IsNested;
        // }

        public static void InitializeXQuinn(Func<Type, string?> toString)
        {
            TypeBook book = CacheXQuinn(toString);
            RuntimeCommand.Register(book.Types, typeof(Core).Module.Assembly.GetName().FullName);
        }
        public static TypeBook CacheXQuinn(Func<Type, string?> toString)
        {
            TypeBook book = TypeBook.New(typeof(Core).Module.GetTypes(), toString, StringComparer.OrdinalIgnoreCase);
            TypeCache.CacheTypes(book);
            return book;
        }

    }
}