using System;
using XQuinn.Reflection;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.LangInterp.SyntaxTree
{
    internal sealed class TypeString : GenericString
    {
        internal static TypeString ThisRef { get; } = new("this");
        internal GenericString? TypeArgOf { get; private init; }
        internal TypeString(string name, GenericString? typeArgOf = null) : base(name)
        {
            TypeArgOf = typeArgOf;
        }
        internal static TypeString New(string name, GenericString? typeArgOf = null)
        {
            return New<TypeString>(new(name, typeArgOf));
        }
        internal Type ToType(TypeBook? dic = null)
        {
            GenericKey key = new(this);
            if (key.Args > 0)
            {
                if (RuntimeCache.ReifiedGenericTypes.TryGetValue(StringID, out Type? type))
                    return type;
                type = ConvertToGeneric(FindType(dic, key), dic);
                RuntimeCache.ReifiedGenericTypes[StringID] = type;
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
            type ??= TypeRegister.GetTypeOrThrow(key);
            return type;
        }
    }
}
