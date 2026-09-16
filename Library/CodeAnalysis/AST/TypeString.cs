using System;
using XQuinn.Reflection;

namespace XQuinn.CodeAnalysis.AST
{
    internal sealed class TypeString : GenericString//, IEquatable<TypeString>
    {
        internal static readonly TypeString s_this = new("this");
        internal readonly GenericString? _typeArgOf; //mostly used for generic lexing, not really necessary to be exposed right now
        //(kind of like paramOf)
        internal TypeString(string name, GenericString? typeArgOf = null) : base(name)
        {
            _typeArgOf = typeArgOf;
        }
        public Type ConvertToGeneric(Type genericTypeDef, TypeBook? types = null)
        {
            return genericTypeDef.MakeGenericType(ConvertGenericArguments(types));
        }


        internal static TypeString New(string name, GenericString? typeArgOf = null)
        {
            return New<TypeString>(new(name, typeArgOf));
        }

        internal static TypeString NewGeneric(string name) //This is for a string that has been confirmed as generic by GenericString.HasTypeArgs()
        {
            TypeString tstring = new(name);
            tstring.UpdateGenericArgs();
            return tstring;
        }

        // bool IEquatable<TypeString>.Equals(TypeString? other)
        // {
        //     return Equals(other);
        // }

        // public override bool Equals(object? obj)
        // {
        //     return obj is TypeString t && Equals(t);
        // }

        // public bool Equals(TypeString? compareTo)
        // {
        //     if (compareTo == null)
        //         return false;
        //     if (compareTo.Generics.Count != Generics.Count)
        //         return false;
        //     if (!compareTo.NameOrValue.EqualsCaseless(NameOrValue))
        //         return false;
        //     for (int i = 0; i < Generics.Count; i++)
        //     {
        //         TypeString myArg = Generics[i];
        //         TypeString theirArg = compareTo.Generics[i];
        //         if (!myArg.Equals(theirArg))
        //             return false;
        //     }
        //     return true;
        // }

        // public override int GetHashCode()
        // {
        //     int hash = 17;
        //     unchecked
        //     {
        //         hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(NameOrValue);
        //         hash = hash * 31 + Generics.Count.GetHashCode();
        //         foreach (TypeString arg in Generics)
        //             hash = hash * 31 + arg.GetHashCode();
        //     }
        //     return hash;
        // }
    }
}