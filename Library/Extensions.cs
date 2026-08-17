using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;

namespace XQuinn.Extensions
{
    public static class StringBuilderExtensions
    {

        public static void AppendMany<T>(this StringBuilder sb, IEnumerable<T?> many, string? divider = null, Func<T?, string?>? toString = null)
        {
            int length = many.Count();
            int i = 0;
            foreach (T? element in many)
            {
                string? text = toString?.Invoke(element);
                if (toString == null) text = element?.ToString();
                sb.Append(text);
                if (divider != null && For.Multiples(length, i)) sb.Append(divider);
                i++;
            }
        }
        public static void CatchException(this StringBuilder sb, Exception ex)
        {
            sb.AppendLine(ex.GetType().ToString());
            sb.AppendLine(ex.Message);
            sb.AppendLine(ex.StackTrace);
            sb.AppendLine(ex.Data.ToString());
            sb.AppendLine(ex.TargetSite?.ToString());
            sb.AppendLine(ex.Source);
        }
    }

    public static class TypeExtensions
    {
        public static string SnipGenericShortName(this Type type)
        {
            if (type.IsGenericTypeDefinition) return type.Name.Remove(type.Name.IndexOf('`')); else return type.Name;
        }
    }

    public static class CollectionExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> objs, Action<T> action)
        {
            foreach (var obj in objs) action(obj);
        }

    }
    public static class StringExtensions
    {
        public static bool EqualsCaseless(this string strng, string? txt)
        {
            return strng.Equals(txt, StringComparison.OrdinalIgnoreCase);
        }
        // /// <summary>
        // /// Remove all occurances of a specified series of characters.
        // /// </summary>
        // public static string RemoveChar(this string text, params char[] chars)
        // {
        //     StringBuilder sb = new();
        //     foreach (char c in text)
        //     {
        //         foreach (var bad in chars)
        //         {
        //             if (bad == c)
        //                 continue;
        //         }
        //         sb.Append(c);
        //     }
        //     return sb.ToString();
        // }

        // public static string ReplaceChar(this string text, char replace, char replacewith)
        // {
        //     StringBuilder sb = new();
        //     foreach (char c in text)
        //     {
        //         sb.Append(c == replace ? replacewith : c);
        //     }
        //     return sb.ToString();
        // }


    }

}