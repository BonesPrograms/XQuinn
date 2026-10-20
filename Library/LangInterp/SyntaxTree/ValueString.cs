using System.Text;
using System;
using HarmonyLib;
using XQuinn.Extensions;
using XQuinn.Parsing;

namespace XQuinn.LangInterp.SyntaxTree
{
    internal sealed class ValueString : ArgumentString
    {
        public string Argument => Arg;
        internal ValueString(string String) : base(String)
        {
        }

        public object? Parse(Type asType)
        {
            if (Arg.EqualsCaseless("default"))
                return asType.GetDefaultValue();
            if (asType.IsClass)
            {
                if (Arg.EqualsCaseless("null"))
                    return null;
                if (AsObjectOr<string>(asType))
                {
                    if (StringFormat(asType == typeof(string), out string? extract))
                        return extract;
                }
                else
                    throw new NotSupportedException($"Cannot convert values to reference type instances. class type: {asType} Value: {Arg}");
            }

            if (asType.IsGenericType && asType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (Arg.EqualsCaseless("null"))
                    return null;
                asType = Nullable.GetUnderlyingType(asType) ?? throw new ArgumentException($"Underlying type for Nullable<T> type {asType} is null.");
            }
            if (asType.IsEnum)
            {
                if (EnumNet20.TryParse(Arg.Replace('|', ','), asType, true, out Enum? @enum))
                    return @enum;
            }
            else if (asType.IsPrimitive || AsObjectOr<decimal>(asType))
            {
                if (ParsePrimitive(asType, out object? primitive))
                    return primitive;
            }
            throw asType.IsPrimitive || asType.IsEnum || AsObjectOr<decimal>(asType) ?
            new FormatException($"Failed to convert {Arg} to {asType}.") :
            new NotSupportedException($"Cannot convert values to user defined struct instances. struct type: {asType}. Value: {Arg}");
        }

        bool StringFormat(bool formatException, out string? extract)
        {
            extract = null;
            if (Argument.Length >= 2)
            {
                string arg = Argument;
                bool noescape = false;
                if (arg[0] == CallLexer.NoEscDeclr)
                {
                    if (arg[1] != CallLexer.StringDeclr)
                        throw new LexicalException("Invalid string format. A quote char must immediately follow the @ char.", arg);
                    noescape = true;
                    arg = arg.Substring(1);
                }
                int lastIndex = arg.Length - 1;
                if (arg[0] == '"' && arg[lastIndex] == '"')
                {
                    if (arg.Length <= 3)
                        extract = arg.Length == 2 ? string.Empty : arg[1].ToString();
                    else
                    {
                        StringBuilder sb = new();
                        bool escaping = false;
                        for (int i = 1; i < lastIndex; i++)
                        {
                            char val = arg[i];
                            if (escaping)
                            {
                                if (val != CallLexer.EscSeq && val != CallLexer.StringDeclr)
                                    throw new LexicalException("Can only escape the quote char \" or escape char \\. ", arg);
                                escaping = false;
                            }
                            else if (val == CallLexer.EscSeq && !noescape)
                            {
                                escaping = true;
                                continue;
                            }
                            else if (val == CallLexer.StringDeclr)
                                throw new LexicalException($"Invalid string format ", arg);
                            sb.Append(arg[i]);
                        }
                        extract = sb.ToString();
                    }
                    return true;
                }
            }
            return formatException ? throw new FormatException($"Strings values must have one beginning and ending quotation mark, and be at least 2 chars in size (including quotations). Value: {Arg}") : false;
        }

        bool ParsePrimitive(Type asType, out object? primitive)
        {
            primitive = null;
            if (AsObjectOr<bool>(asType))
            {
                if (bool.TryParse(Arg, out bool boolean))
                {
                    primitive = boolean;
                    return true;
                }
            }
            if (AsObjectOr<int>(asType))
            {
                if (int.TryParse(Arg, out int sint32))
                {
                    primitive = sint32;
                    return true;
                }
            }
            if (AsObjectOr<uint>(asType))
            {
                if (uint.TryParse(Arg, out uint uint32))
                {
                    primitive = uint32;
                    return true;
                }
            }
            if (AsObjectOr<long>(asType))
            {
                if (long.TryParse(Arg, out long sint64))
                {
                    primitive = sint64;
                    return true;
                }
            }
            if (AsObjectOr<ulong>(asType))
            {
                if (ulong.TryParse(Arg, out ulong uint64))
                {
                    primitive = uint64;
                    return true;
                }
            }
            if (AsObjectOr<float>(asType))
            {
                if (float.TryParse(Arg, out float float32))
                {
                    primitive = float32;
                    return true;
                }
            }
            if (AsObjectOr<double>(asType))
            {
                if (double.TryParse(Arg, out double float64))
                {
                    primitive = float64;
                    return true;
                }
            }
            if (AsObjectOr<decimal>(asType))
            {
                if (decimal.TryParse(Arg, out decimal dec))
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
            }
            if (asType == typeof(object))
                return false;
            else if (asType == typeof(nint))
            {
                if (NIntNet20.TryParse(Arg, out nint nativesint))
                    primitive = nativesint;
            }
            else if (asType == typeof(nuint))
            {
                if (NUIntNet20.TryParse(Arg, out nuint nativeuint))
                    primitive = nativeuint;
            }
            else if (asType == typeof(byte))
            {
                if (byte.TryParse(Arg, out byte uint8))
                    primitive = uint8;
            }
            else if (asType == typeof(sbyte))
            {
                if (sbyte.TryParse(Arg, out sbyte sint8))
                    primitive = sint8;
            }
            else if (asType == typeof(short))
            {
                if (short.TryParse(Arg, out short sint16))
                    primitive = sint16;
            }
            else if (asType == typeof(ushort))
            {
                if (ushort.TryParse(Arg, out ushort uint16))
                    primitive = uint16;
            }
            return primitive != null;
        }

        bool CharTryParse(out char utf16, bool formatException)
        {
            utf16 = default;
            if (Arg.Length == 3)
            {
                if (Arg[0] == '\'' && Arg[2] == '\'')
                {
                    utf16 = Arg[1];
                    return true;
                }
            }
            return formatException ? throw new FormatException($"Invalid char format. Input: {Arg}. Must be surrounzed by apostrophes, must be a single char.") : false;
        }
        static bool AsObjectOr<T>(Type asType) => typeof(T) == asType || typeof(object) == asType;
    }
}
