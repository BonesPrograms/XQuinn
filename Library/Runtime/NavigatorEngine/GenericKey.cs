using System.Reflection;
using System;
using System.Text;
using XQuinn.CodeAnalysis.AST;
using XQuinn.Extensions;

namespace XQuinn.Runtime.NavigatorEngine
{
    internal readonly struct GenericKey : IEquatable<GenericKey>
    {
        public readonly string Name;
        public readonly int Args;
        public static GenericKey TypeQuery(string typename)
        {
            if (GenericString.HasTypeArgs(typename))
            {
                TypeString query = TypeString.NewGeneric(typename);
                GenericKey key = new(query);
                return key;
            }
            return new(typename);
        }


        public GenericKey(string key, MethodBase m) : this(key, m.IsGenericMethodDefinition ? m.GetGenericArguments().Length : 0)
        {
        }

        public GenericKey(string key, int args = 0)
        {
            Name = key;
            Args = args;
        }

        public GenericKey(string key, Type t) : this(key, t.IsGenericTypeDefinition ? t.GetGenericArguments().Length : 0)
        {
        }


        internal GenericKey(TypeString str) : this(str.Name, str.Generics.Count)
        {
        }

        bool IEquatable<GenericKey>.Equals(GenericKey other)
        {
            return Equals(other);
        }

        public override bool Equals(object? obj)
        {
            return obj is GenericKey key && Equals(key);
        }

        public bool Equals(GenericKey key)
        {
            return Args == key.Args && Name.EqualsCaseless(key.Name);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
                hash = hash * 23 + Args.GetHashCode();
            }
            return hash;
        }

        public static bool operator ==(GenericKey left, GenericKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GenericKey left, GenericKey right)
        {
            return !(left == right);
        }

        internal string ArgsToString(StringBuilder sb)
        {
            sb.Append("<T1, ");
            for (int i = 1; i < Args; i++)
            {
                sb.Append($"T{i + 1}");
                if (For.NeedsDelimiter(Args, i))
                    sb.Append(", ");
            }
            sb.Append('>');
            return sb.ToString();
        }

        public override string ToString()
        {
            if (Args > 0)
            {
                if (Args == 1)
                    return $"{Name}<T>";
               return ArgsToString(new(Name));

            }
            return Name;
        }

    }
}