using System;
using XQuinn.Reflection;
using XQuinn.Runtime.NavigatorEngine;

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
        internal static TypeString New(string name, GenericString? typeArgOf = null)
        {
            return New<TypeString>(new(name, typeArgOf));
        }

        internal static TypeString NewGeneric(string name) //This is for a string that has been externally confirmed as generic by GenericString.HasTypeArgs()
        {
            TypeString tstring = new(name);
            tstring.UpdateGenericArgs();
            return tstring;
        }
        internal Type ToType(TypeBook? dic = null)
        {
            GenericKey key = new(this);
            if (key.Args > 0)
            {
                if (RuntimeCache.s_reified_generic_types.TryGetValue(StringID, out Type? type))
                    return type;
                type = ConvertToGeneric(FindType(dic, key), dic);
                RuntimeCache.s_reified_generic_types[StringID] = type;
                return type;
            }
            return FindType(dic, key);

        }
        Type ConvertToGeneric(Type genericTypeDef, TypeBook? types = null)
        {
            return genericTypeDef.MakeGenericType(ConvertGenericArguments(types));
        }

        static Type FindType(TypeBook? dic, GenericKey key)
        {
            Type? type = null;
            dic?.TryGetValue(key, out type);
            type ??= TypeCache.GetTypeOrThrow(key);
            return type;
        }
    }
}