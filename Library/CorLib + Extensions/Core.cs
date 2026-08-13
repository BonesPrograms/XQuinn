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
            if (TypeCache.TryGetType(t.Name, out Type? cached))
            {
                if (cached == t)
                    return null;
                else
                {
                    string snipped = SnipGenericShortName(t);
#if NET6_0_OR_GREATER
                    _changedNames ??= new(StringComparer.OrdinalIgnoreCase);
                    _namesreadonly ??= new Net6ReadOnlySet<string>(_changedNames);
                    _changedNames.Add(snipped);
#endif
                    return $"xq_{snipped}";
                }
            }
            return t.IsPublic ? SnipGenericShortName(t) : null;
        }

        public static string SnipGenericShortName(Type t)
        {
            if (t.IsGenericTypeDefinition)
            {
                return t.Name.Remove(t.Name.IndexOf('`'));
            }
            return t.Name;
        }

        // public static bool DefaultWhere(Type t)
        // {
        //     if (TypeCache.TryGetValue(t.Name, out Type? cached) && cached == t) //skip already cached xquinn types (if that ever happens)
        //         return false;
        //     return t.IsPublic && !t.IsNested;
        // }
        public static TypeBook CacheXQuinn(Func<Type, string?> toString)
        {
            TypeBook book = TypeBook.New(typeof(Core).Module.GetTypes(), toString, StringComparer.OrdinalIgnoreCase);
            TypeCache.CacheTypes(book);
            return book;
        }

    }
}