using System.Text;
using System;
using HarmonyLib;
using XQuinn.Extensions;
using XQuinn.Parsing;

namespace XQuinn.CodeAnalysis.AST
{
    internal sealed class ValueString : ParameterString
    {
        internal ValueString(string String) : base(String)
        {

        }
        ///This does not work with FieldString,MethodString or TypeString. it is for true ParameterStrings
        public object? Parse(Type asType)
        {
            if (NameOrValue.EqualsCaseless("default"))
                return asType.GetDefaultValue();
            if (asType.IsClass)
            {
                if (NameOrValue.EqualsCaseless("null"))
                    return null;
                if (AsObjectOr<string>(asType))
                {
                    if (IsFormattedLikeAString(asType == typeof(string), out string? extract))
                        return extract;
                }
                else
                    throw new NotSupportedException($"Cannot convert values to reference type instances. class type: {asType} Value: {NameOrValue}");
            }
            // if (asType.IsValueType || asType == typeof(object))
            if (asType.IsGenericType && asType.GetGenericTypeDefinition() == typeof(Nullable<>)) //ValueType of Nullable<T>
            {
                if (NameOrValue.EqualsCaseless("null"))
                    return null;
                asType = Nullable.GetUnderlyingType(asType) ?? throw new ArgumentException($"Underlying type for Nullable<T> type {asType} is null.");
            }
            if (asType.IsEnum)
            {
                if (EnumNet20.TryParse(NameOrValue.Replace('|', ','), asType, true, out Enum? @enum))
                    return @enum;
            }
            else if (asType.IsPrimitive || AsObjectOr<decimal>(asType))
            {
                if (ParsePrimitive(asType, out object? primitive))
                    return primitive;
            }
            throw asType.IsPrimitive || asType == typeof(object) ?
            new FormatException($"Failed to convert {NameOrValue} to {asType}.") :
            new NotSupportedException($"Cannot convert values to user defined struct instances. struct type: {asType}. Value: {NameOrValue}");

        }

        bool IsFormattedLikeAString(bool formatException, out string? extract)
        {
            extract = null;
            if (NameOrValue.Length > 2)
            {
                int lastIndex = NameOrValue.Length - 1;
                if (NameOrValue[0] == '"' && NameOrValue[lastIndex] == '"')
                {
                    if (NameOrValue.Length <= 3)
                        extract = NameOrValue.Length == 2 ? string.Empty : NameOrValue[1].ToString(); //size can only be 2 or 3, first and last index are removed
                    else
                    {
                        StringBuilder sb = new();
                        for (int i = 1; i < lastIndex; i++)
                            sb.Append(NameOrValue[i]);
                        extract = sb.ToString();
                    }
                    return true;
                }
            }
            return formatException ? throw new FormatException($"Strings values must have one beginning and ending quotation mark, and be at least 2 chars in size (including quotations). Value: {NameOrValue}") : false;
        }

        bool ParsePrimitive(Type asType, out object? primitive) //Almost looks like a switch!
        {
            primitive = null;
            if (AsObjectOr<bool>(asType))
            {
                if (bool.TryParse(NameOrValue, out bool boolean))
                {
                    primitive = boolean;
                    return true;
                }
            }
            if (AsObjectOr<int>(asType))
            {
                if (int.TryParse(NameOrValue, out int sint32))
                {
                    primitive = sint32;
                    return true;
                }
            }
            if (AsObjectOr<uint>(asType))
            {
                if (uint.TryParse(NameOrValue, out uint uint32))
                {
                    primitive = uint32;
                    return true;
                }
            }
            if (AsObjectOr<long>(asType))
            {
                if (long.TryParse(NameOrValue, out long sint64))
                {
                    primitive = sint64;
                    return true;
                }
            }
            if (AsObjectOr<ulong>(asType))
            {
                if (ulong.TryParse(NameOrValue, out ulong uint64))
                {
                    primitive = uint64;
                    return true;
                }
            }
            if (AsObjectOr<float>(asType))
            {
                if (float.TryParse(NameOrValue, out float float32))
                {
                    primitive = float32;
                    return true;
                }
            }
            if (AsObjectOr<double>(asType))
            {
                if (double.TryParse(NameOrValue, out double float64))
                {
                    primitive = float64;
                    return true;
                }
            }
            if (AsObjectOr<decimal>(asType))
            {
                if (decimal.TryParse(NameOrValue, out decimal dec))
                {
                    primitive = dec;
                    return true;
                }
            }
            if (AsObjectOr<char>(asType))
            {
                if (CharTryParse(out char utf16, asType == typeof(char)))
                {
                    primitive = utf16;
                    return true;
                }
            } //for (object) we check char last because byte sized integers easily convert to char
            if (asType == typeof(object))
                return false;
            else if (asType == typeof(nint))
            {
                if (NIntNet20.TryParse(NameOrValue, out nint nativesint))
                    primitive = nativesint;
            }
            else if (asType == typeof(nuint))
            {
                if (NUIntNet20.TryParse(NameOrValue, out nuint nativeuint))
                    primitive = nativeuint;
            }
            else if (asType == typeof(byte))
            {
                if (byte.TryParse(NameOrValue, out byte uint8))
                    primitive = uint8;
            }
            else if (asType == typeof(sbyte))
            {
                if (sbyte.TryParse(NameOrValue, out sbyte sint8))
                    primitive = sint8;
            }
            else if (asType == typeof(short))
            {
                if (short.TryParse(NameOrValue, out short sint16))
                    primitive = sint16;
            }
            else if (asType == typeof(ushort))
            {
                if (ushort.TryParse(NameOrValue, out ushort uint16))
                    primitive = uint16;
            }
            return primitive != null;
        }

        bool CharTryParse(out char utf16, bool formatException)
        {
            utf16 = default;
            if (NameOrValue.Length == 3)
            {
                if (NameOrValue[0] == '\'' && NameOrValue[2] == '\'')
                {
                    utf16 = NameOrValue[1];
                    return true;
                }
            }
            return formatException ? throw new FormatException($"Invalid char format. Input: {NameOrValue}. Must be surrounzed by apostrophes, must be a single char.") : false;


        }
        static bool AsObjectOr<T>(Type asType) => typeof(T) == asType || typeof(object) == asType;

    }
}