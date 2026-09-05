using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using XQuinn.Reflection;
using HarmonyLib;
using XQuinn.Extensions;
using XQuinn.Parsing;
using System.Collections.ObjectModel;
using XQuinn;

namespace XQuinn.CodeAnalysis.AST
{

    internal interface IMemberString
    {
        public TypeString DeclaringType { get; }
    }


    //This is tightly coupled to CallInterpreter. It can be used elsewhere (I use it in RuntimeCommands) but actually turning parameters to objects or generic parameters to type arrays
    //relies on methods in CallInterpreter. I may decouple it later, but I found it difficult - CallInterpreter.GetGenerics() relies on CallInterpreter.
    // FindType(), and I think it is just weird to encapsulate
    //FindType(TypeString) behind GenericParameter, it should be encapsulated as part of the TypeString class, but GenericParameter would need access to that method, its just a mess rn lol
    //Parameter doesnt support Generics so you cannot just move it all up to the top class
    internal abstract class ParameterString
    {
        public string NameOrValue
        {
            get => _string;
            protected set => _string = value;
        }

        string _string;
        internal ParameterString(string String)
        {
            _string = String;
        }
        public override string ToString()
        {
            return NameOrValue;
        }


    }

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
            else if (asType.IsPrimitive || asType == typeof(object))
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

    internal sealed class FieldString : ParameterString, IMemberString
    {
        public TypeString DeclaringType => _type;
        readonly TypeString _type;
        internal FieldString(string name, TypeString declaredIn) : base(name.Trim())
        {
            _type = declaredIn;
        }

    }

    internal abstract class GenericString : ParameterString
    {

        public string NameWithGenerics => _fullname;
        string _fullname;
        public IReadOnlyList<TypeString> Generics
        {
            get
            {
                return _type_args == null ? Array.Empty<TypeString>() : _type_args;
            }
        }
        List<TypeString>? _type_args;
        internal GenericString(string nameForSnipping) : base(nameForSnipping)
        {
            _fullname = nameForSnipping;
        }
        internal static T New<T>(T genericString, bool dontLex = false) where T : GenericString
        {
            if (!dontLex)
            {
                if (genericString.HasTypeArgs())
                {
                    StringBuilder sb = new();
                    genericString.LexGenerics(sb);
                    sb.Length = 0;
                    genericString.PrintTypeArgs(sb);
                }
            }
            return genericString;
        }

        bool HasTypeArgs()
        {
            bool genericStart = false;
            for (int i = 0; i < NameOrValue.Length; i++)
            {
                char c = NameOrValue[i];
                if (c == '<') genericStart = true;
                else if (c == '>') return genericStart ? true : throw new FormatException($"Invalid generic argument format. {NameOrValue}");
            }
            return genericStart ? throw new FormatException($"Invalid generic argument format. {NameOrValue}") : false;
        }
        public Type[] ConvertGenericArguments(IReadOnlyDictionary<string, Type>? dic = null)
        {
            if (Generics.Count > 0)
            {
                Type[] genericArgs = new Type[Generics.Count];
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    TypeString tstring = Generics[i];
                    Type realtype = TypeCache.GetTypeOrThrow(tstring.NameOrValue, dic);
                    if (realtype.IsGenericTypeDefinition) realtype = realtype.MakeGenericType(tstring.ConvertGenericArguments(dic));
                    genericArgs[i] = realtype;
                }
                return genericArgs;
            }
            throw new ArgumentException($"No generic parameters were provided to {GetType().Name} with name value {NameOrValue} ");
        }
        void LexGenerics(StringBuilder sb)
        {
            GenericString currentGeneric = this;
            bool finishedReadingLeadName = false;
            for (int i = 0; i < _fullname.Length; i++) //this ones a lot simpler doesnt have smart whitespace skipping and doesnt actually check for context like
            {                                           //whether or not its reading a proper identifier and not 83528474
                char c = _fullname[i];                  //kinda busted it out quickly so it will allow things that the invocationlexer would throw for
                if (c == ' ')
                    continue;                 //like collecting < s t r i n g> into string or allowing impossible names to lex
                if (c == '<')
                {
                    if (finishedReadingLeadName)
                        currentGeneric = currentGeneric.AddTypeArg(sb);
                    else
                    {
                        currentGeneric.NameOrValue = sb.ToString();
                        sb.Length = 0;
                        finishedReadingLeadName = true;
                    }
                }
                else if (c == '>')
                {
                    if (sb.Length > 0)
                    {
                        currentGeneric.AddTypeArg(sb);
                        if (currentGeneric != this)
                        {
                            TypeString currentArg = (TypeString)currentGeneric;
                            currentGeneric = currentArg._typeArgOf!;
                            continue;
                        }
                        break;
                    }
                }
                else if (c == ',')
                {
                    if (sb.Length > 0)
                        currentGeneric.AddTypeArg(sb);
                }
                else sb.Append(c);
            }
        }

        TypeString AddTypeArg(StringBuilder sb)
        {
            string name = sb.ToString();
            sb.Length = 0;
            _type_args ??= new List<TypeString>();
            TypeString typearg = TypeString.New(name, this, true);
            _type_args.Add(typearg);
            return typearg;
        }


        public override string ToString()
        {
            return _fullname;
        }

        void PrintTypeArgs(StringBuilder sb)
        {
            sb.Append(NameOrValue);
            sb.Append('<');
            sb.AppendMany(Generics, ",");
            sb.Append('>');
            _fullname = sb.ToString();
        }




    }

    internal sealed class TypeString : GenericString
    {

        internal static readonly TypeString s_this = new("this");

        internal readonly GenericString? _typeArgOf; //mostly used for generic lexing, not really necessary to be exposed right now
        //(kind of like paramOf)
        TypeString(string nameForSnipping, GenericString? typeArgOf = null) : base(nameForSnipping.Trim())
        {
            _typeArgOf = typeArgOf;
        }
        public Type ConvertToGeneric(Type genericTypeDef, IReadOnlyDictionary<string, Type>? types = null)
        {
            //Type t;
            return genericTypeDef.MakeGenericType(ConvertGenericArguments(types));
        }
        // catch
        // {
        //     StringBuilder sb = new();
        //     sb.AppendMany(_generics, ", ");
        //     throw;
        // }
        // return t;


        internal static TypeString New(string name, GenericString? typeArgOf = null, bool fromLex = false)
        {
            return New<TypeString>(new(name, typeArgOf), fromLex);
        }

    }
    internal sealed class MethodString : GenericString, IMemberString
    {
        internal readonly MethodString? _subParamOf;
        public TypeString DeclaringType => _type;
        readonly TypeString _type;
        public IReadOnlyList<ParameterString> Params
        {
            get
            {
                return _args == null ? Array.Empty<ParameterString>() : _args;
            }
        }
        List<ParameterString>? _args; //= Array.Empty<ParameterString>();
        internal static MethodString New(string name, MethodString? paramOf, TypeString declaredIn)
        {
            return New<MethodString>(new(name, paramOf, declaredIn));
        }
        MethodString(string nameForSnipping, MethodString? paramOf, TypeString type) : base(nameForSnipping)
        {
            _subParamOf = paramOf;
            _type = type;
        }

        public MethodInfo ConvertToGeneric(MethodInfo genericMethodDef, IReadOnlyDictionary<string, Type>? types = null)
        {
            //  Console.WriteLine("TConversion");
            //MethodInfo m;
            return genericMethodDef.MakeGenericMethod(ConvertGenericArguments(types));
        }
        //     catch
        //     {
        //         StringBuilder sb = new();
        //         sb.AppendMany(_generics, ", ");
        //         throw;
        //     }
        //     return m;
        // }
        internal void AddParameter(ParameterString param)
        {
            _args ??= new List<ParameterString>();
            _args.Add(param);
        }

        void ParamStringShort(StringBuilder sb)
        {
            sb.Append(NameWithGenerics);
            sb.Append("( ");
            AppendParams(sb);
            sb.Append(" )");

        }

        void AppendParams(StringBuilder sb)
        {
            StringBuilder sb2 = new();
            sb.AppendMany(Params, ", ", false, x =>
            {
                if (x is MethodString ms)
                {
                    ms.ParamStringShort(sb2);
                    string s = sb2.ToString();
                    sb2.Length = 0;
                    return s;
                }
                else return x!.NameOrValue;
            });
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(NameWithGenerics);
            sb.Append($" :: Nested in ");
            _subParamOf?.ParamStringShort(sb);
            sb.Append(" :: ");
            sb.Append($"TypeName {DeclaringType.NameWithGenerics} :: ");
            sb.Append("Params: ");
            if (Params.Count > 0) AppendParams(sb);
            return sb.ToString();

        }
    }
}