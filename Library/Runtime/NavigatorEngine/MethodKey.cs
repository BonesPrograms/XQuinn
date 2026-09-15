using System;
using XQuinn.Reflection;
using XQuinn.CodeAnalysis.AST;
using System.Text;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal readonly struct MethodKey : IEquatable<MethodKey>
    {
        public readonly int OverloadIndex;
        public readonly GenericKey GenericKey;
        internal MethodKey(int index, GenericKey key)
        {
            OverloadIndex = index;
            GenericKey = key;
        }

        internal static MethodKey MethodQuery(MethodString mthdString)
        {
            string name = mthdString.NameOrValue;
            int split = name.IndexOf(':');
            //  string[] query = name.Split(':');
            int index = 0;
            if (split >= 0)
            {
                string indexed = name.Substring(split + 1); //stringSplit better here? prob doestn amtter
                index = int.Parse(indexed);
                name = name.Remove(split);
            }
            GenericKey key = new(name, mthdString.Generics.Count);
            return new(index, key);
        }

        bool IEquatable<MethodKey>.Equals(MethodKey obj)
        {
            return Equals(obj);
        }
        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 31 + OverloadIndex.GetHashCode();
                hash = hash * 31 + GenericKey.GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            StringBuilder sb = new(GenericKey.Key);
            if (OverloadIndex > 0)
                sb.Append($":{OverloadIndex}");
            return GenericKey.Args == 0 ? sb.ToString() : GenericKey.Args == 1 ? sb.Append("<T>").ToString() : GenericKey.ArgsToString(sb);
        }
        public bool Equals(MethodKey overload)
        {
            return overload.OverloadIndex == OverloadIndex && overload.GenericKey == GenericKey;
        }

        public override bool Equals(object? obj)
        {
            return obj is MethodKey overload && Equals(overload);
        } //obj is Overload overload && ((IEquatable<Overload>)this).Equals(overload);

        public static bool operator ==(MethodKey left, MethodKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MethodKey left, MethodKey right)
        {
            return !(left == right);
        }
    }
}