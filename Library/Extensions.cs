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
                string? text;
                if (toString != null)
                    text = toString.Invoke(element);
                else
                    text = element?.ToString();
                sb.Append(text);
                if (divider != null && For.Multiples(length, i))
                    sb.Append(divider);
                i++;
            }
        }
        public static void CatchException(this StringBuilder sb, Exception ex)
        {
            sb.Append($"{ex.GetType()}{Environment.NewLine}");
            sb.Append($"{ex.Message}{Environment.NewLine}");
            sb.Append($"{ex.StackTrace}{Environment.NewLine}");
            sb.Append($"{ex.Data}{Environment.NewLine}{ex.TargetSite}{Environment.NewLine}{ex.Source}{Environment.NewLine}");
        }
    }

    public static class TypeExtensions
    {
        public static string SnipGenericShortName(this Type t)
        {
            if (t.IsGenericTypeDefinition)
            {
                return t.Name.Remove(t.Name.IndexOf('`'));
            }
            return t.Name;
        }
    }

    public static class CollectionExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> objs, Action<T> action)
        {
            foreach (var obj in objs)
                action(obj);
        }

    }
    public static class StringExtensions
    {
        public static bool EqualsCaseless(this string strng, string? txt)
        {
            return strng.Equals(txt, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Remove all occurances of a specified series of characters.
        /// </summary>
        public static string RemoveChar(this string text, params char[] chars)
        {
            StringBuilder sb = new();
            foreach (char c in text)
            {
                if (!chars.Any(x => x == c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string ReplaceChar(this string text, char replace, char replacewith)
        {
            StringBuilder sb = new();
            foreach (char c in text)
            {
                sb.Append(c == replace ? replacewith : c);
            }
            return sb.ToString();
        }


    }

}