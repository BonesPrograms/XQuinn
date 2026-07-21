
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


}

