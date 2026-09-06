using System;
using XQuinn.Extensions;

namespace XQuinn.Private.EqualityHelpers
{

    internal interface IIndexKeyPair<T> where T : struct
    {
        public string Key { get; }
        public int Index { get; }
    }
    internal static class IndexKeyPair<T> where T : struct, IIndexKeyPair<T>
    {
        public static int HashCode(int mult, T data)
        {
            int hash = 17;
            unchecked
            {
                hash = hash * mult + StringComparer.OrdinalIgnoreCase.GetHashCode(data.Key);
                hash = hash * mult + data.Index.GetHashCode();
            }
            return hash;
        }

        public static bool Equals(T left, T right)
        {
            return left.Index == right.Index && left.Key.EqualsCaseless(right.Key);
        }

    }
}