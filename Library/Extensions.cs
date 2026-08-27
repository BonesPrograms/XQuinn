using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Collections;
using XQ.Reflection;

namespace XQ.Extensions
{



    public static class CharExtensions
    {
        public static bool IsLetter(this char value) => value switch
        {
            >= 'a' and <= 'z' or >= 'A' and <= 'Z' => true,
            _ => false
        };

        public static bool IsDigit(this char value) => value switch
        {
            >= '0' and <= '9' => true,
            _ => false
        };
    }
    public static class StringBuilderExtensions
    {

        public static StringBuilder AppendMany(this StringBuilder sb, IEnumerable many, string? divider = null, Func<object?, string?>? toString = null)
        {
            int length;
            if (many is ICollection col)
                length = col.Count;
            else
                checked
                {
                    length = 0;
                    foreach (object? element in many)
                        length++;
                }
            int i = 0;
            foreach (object? element in many)
                AppendMany<object>(length, ref i, sb, element, divider, toString);
            return sb;
        }
        public static StringBuilder AppendMany<T>(this StringBuilder sb, IEnumerable<T?> many, string? divider = null, Func<T?, string?>? toString = null)
        {
            int length = many.Count();
            int i = 0;
            foreach (T? element in many)
                AppendMany<T>(length, ref i, sb, element, divider, toString);
            return sb;
        }

        static void AppendMany<T>(int length, ref int i, StringBuilder sb, T? element, string? divider = null, Func<T?, string?>? toString = null)
        {
            string? text = toString?.Invoke(element);
            if (toString == null) text = element?.ToString();
            sb.Append(text ?? "null");
            if (divider != null && For.Multiples(length, i)) sb.Append(divider);
            i++;
        }
        public static void CatchException(this StringBuilder sb, Exception ex)
        {
            sb.AppendLine($"Exception Type: {ex.GetType()}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"Inner Exception: {ex.InnerException?.ToString() ?? "<none>"}");
            sb.AppendLine($"Stack Trace: {ex.StackTrace}");
            sb.AppendLine($"Data: {ex.Data}");
            sb.AppendLine($"Target Site: {ex.TargetSite?.ToString() ?? "<none>"}");
            sb.AppendLine($"Source: {ex.Source ?? "<none>"}");
        }
    }



    // public static class CollectionExtensions
    // {
    //     public static void ForEach<T>(this IEnumerable<T> objs, Action<T> action)
    //     {
    //         foreach (var obj in objs) action(obj);
    //     }

    public static class StringExtensions
    {

#if NET6_0_OR_GREATER
        public static bool EqualsCaseless(this string strng, ReadOnlySpan<char> txt) => txt.Equals(strng, StringComparison.OrdinalIgnoreCase);

#else
        public static bool EqualsCaseless(this string strng, string txt)
        {
            return strng.Equals(txt, StringComparison.OrdinalIgnoreCase);
        }
#endif
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

        //         public static string ReplaceChar(this string text, char replace, char replacewith)
        //         {
        // #if NET10_0_OR_GREATER
        //             ReadOnlySpan<char> span = text.AsSpan();
        //             Span<char> buffer = stackalloc char[text.Length];
        //             span.CopyTo(buffer);
        //             for(int i = 0; i < buffer.Length; i++)
        //             {
        //                 char c = buffer[i];
        //                 if(c == replace)
        //                 buffer[i] = replacewith;
        //             }
        //             return buffer.ToString();
        // #else
        //             StringBuilder sb = new();
        //             foreach (char c in text) sb.Append(c == replace ? replacewith : c);
        //             return sb.ToString();
        // #endif
        //         }


    }

}