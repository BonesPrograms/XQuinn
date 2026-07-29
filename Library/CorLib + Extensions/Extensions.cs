using System.Text;

namespace XQuinn.Extensions;

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
            if (!chars.Contains(c))
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

