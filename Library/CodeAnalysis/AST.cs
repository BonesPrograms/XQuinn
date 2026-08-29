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
        public TypeString? DeclaringType { get; }
    }
    //"abstract syntax tree"

    //This is tightly coupled to CallInterpreter. It can be used elsewhere (I use it in RuntimeCommands) but actually turning parameters to objects or generic parameters to type arrays
    //relies on methods in CallInterpreter. I may decouple it later, but I found it difficult - CallInterpreter.GetGenerics() relies on CallInterpreter.
    // FindType(), and I think it is just weird to encapsulate
    //FindType(TypeString) behind GenericParameter, it should be encapsulated as part of the TypeString class, but GenericParameter would need access to that method, its just a mess rn lol
    //Parameter doesnt support Generics so you cannot just move it all up to the top class
    internal abstract class ParameterString
    {
        public string String { get => _string; protected set => _string = value; }

        string _string;
        internal ParameterString(string String)
        {
            _string = String;
        }
        public override string ToString()
        {
            return String;
        }


    }

    internal sealed class ValueString : ParameterString
    {
        internal ValueString(string String) : base(String)
        {

        }
        ///This does not work with FieldString,MethodString or TypeString. it is for true ParameterStrings
        public object? ParseValue(Type asType)
        {

            if (String.EqualsCaseless("default"))
                return asType.GetDefaultValue();
            if (asType.IsGenericType && asType.GetGenericTypeDefinition() == typeof(Nullable<>)) //ValueType of Nullable<T>
            {
                if (String.EqualsCaseless("null"))
                    return null;
                asType = Nullable.GetUnderlyingType(asType) ?? throw new ArgumentException($"Underlying type for Nullable<T> type {asType} is null.");
            }
            if (asType.IsClass)
            {
                if (String.EqualsCaseless("null"))
                    return null;
                if (asType == typeof(string) || asType == typeof(object))
                {
                    if (CheckForString(asType == typeof(string), out string? extract))
                        return extract;
                }
                else throw new NotSupportedException($"Cannot convert values to reference type instances. class type: {asType} Value: {String}");
            }
            if (asType.IsValueType || asType == typeof(object))
            {
                if (asType.IsEnum)
                {
                    if (EnumNet20.TryParse(String.Replace('|', ','), asType, true, out Enum? @enum))
                        return @enum;
                }
                else if (asType.IsPrimitive || asType == typeof(object))
                {
                    if (ParsePrimitive(asType, out ValueType? primitive))
                        return primitive;
                }
                else throw new NotSupportedException($"Cannot convert values to user defined struct instances. struct type: {asType}. Value: {String}");
            }
            throw new FormatException($"Failed to convert {String} to {asType}.");
        }

        bool CheckForString(bool formatException, out string? extract)
        {
            extract = null;
            if (String.Length < 2)
                return formatException ? throw new FormatException("Strings values must be at least 2 chars in size - one beginning and one ending quotation mark.") : false;
            if (String.Length == 2)
            {
                if (String[0] == '"' && String[1] == '"')
                {
                    extract = string.Empty;
                    return true;
                }
                else if (formatException)
                    throw new FormatException("Strings values must be at least 2 chars in size - one beginning and one ending quotation mark.");
            }
            if (String.Length == 3)
            {
                if (String[0] == '"' && String[2] == '"')
                {
                    extract = String[1].ToString();
                    return true;
                }
                else if (formatException)
                    throw new FormatException("Strings values must be at least 2 chars in size - one beginning and one ending quotation mark.");
            }
            int end = String.Length - 1;
            if (String[0] == '"' && String[end] == '"')
            {
                StringBuilder sb = new();
                for (int i = 1; i < end; i++)
                    sb.Append(String[i]);
                extract = sb.ToString();
                return true;
            }
            return formatException ? throw new FormatException($"Strings values must be at least 2 chars in size - one beginning and one ending quotation mark. Input {String}") : false;
        }
        bool ParsePrimitive(Type asType, out ValueType? primitive) //Almost looks like a switch!
        {
            primitive = null;
            if (asType == typeof(object) || asType == typeof(bool))
            {
                if (bool.TryParse(String, out bool boolean))
                {
                    primitive = boolean;
                    return true;
                }
            }
            if (asType == typeof(object) || asType == typeof(int))
            {
                if (int.TryParse(String, out int sint32))
                {
                    primitive = sint32;
                    return true;
                }
            }
            if (asType == typeof(object) || asType == typeof(uint))
            {
                if (uint.TryParse(String, out uint uint32))
                {
                    primitive = uint32;
                    return true;
                }
            }
            if (asType == typeof(object) || asType == typeof(long))
            {
                if (long.TryParse(String, out long sint64))
                {
                    primitive = sint64;
                    return true;
                }
            }
            if (asType == typeof(object) || asType == typeof(ulong))
            {
                if (ulong.TryParse(String, out ulong uint64))
                {
                    primitive = uint64;
                    return true;
                }
            }
            if (asType == typeof(object) || asType == typeof(float))
            {
                if (float.TryParse(String, out float float32))
                {
                    primitive = float32;
                    return true;
                }
            }
            if (asType == typeof(object) || asType == typeof(double))
            {
                if (double.TryParse(String, out double float64))
                {
                    primitive = float64;
                    return true;
                }
            }
            if (asType == typeof(object) || asType == typeof(decimal))
            {
                if (decimal.TryParse(String, out decimal dec))
                {
                    primitive = dec;
                    return true;
                }
            }
            if (asType == typeof(object) || asType == typeof(char))
            {
                if (char.TryParse(String, out char utf16))
                {
                    primitive = utf16;
                    return true;
                }
            } //for (object) we check char last because byte sized integers easily convert to char
            if (asType == typeof(nint))
            {
                if (NIntNet20.TryParse(String, out nint nativesint))
                    primitive = nativesint;
            }
            else if (asType == typeof(nuint))
            {
                if (NUIntNet20.TryParse(String, out nuint nativeuint))
                    primitive = nativeuint;
            }
            else if (asType == typeof(byte))
            {
                if (byte.TryParse(String, out byte uint8))
                    primitive = uint8;
            }
            else if (asType == typeof(sbyte))
            {
                if (sbyte.TryParse(String, out sbyte sint8))
                    primitive = sint8;
            }
            else if (asType == typeof(short))
            {
                if (short.TryParse(String, out short sint16))
                    primitive = sint16;
            }
            else if (asType == typeof(ushort))
            {
                if (ushort.TryParse(String, out ushort uint16))
                    primitive = uint16;
            }
            return primitive != null;
        }

        //// bool TryParseChar(out char utf16)
        // {
        //if (String.Length != 3) throw new FormatException($"Invalid char format. Input: {String}. Must be surrounzed by apostrophes, must be a single char.");
        //  return char.TryParse(String[2].ToString(), out utf16);
        //}

    }

    internal sealed class FieldString : ParameterString, IMemberString
    {
        public TypeString DeclaringType => _type;
        TypeString _type;
        internal FieldString(string name, TypeString declaredIn) : base(name.Trim())
        {
            _type = declaredIn;
        }

    }

    internal abstract class GenericString : ParameterString
    {
        public string NameWithGenerics => _fullname;
        string _fullname;
        internal List<TypeString> _generics = new();
        private protected GenericString(string nameForSnipping, string fullNamePreserved) : base(nameForSnipping)
        {
            _fullname = fullNamePreserved;//.Trim();
        }
        private protected static T New<T>(T genericString, bool fromLex = false) where T : GenericString
        {
            if (!fromLex)
            {
                if (CheckForGenericArguments(genericString.String))
                {
                    genericString.LexGenerics();
                    genericString._fullname = genericString.PrintGenericArgs();
                }
            }
            return genericString;
        }

        static bool CheckForGenericArguments(string name)
        {
            bool genericStart = false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '<') genericStart = true;
                else if (c == '>') return genericStart ? true : throw new FormatException($"Invalid generic argument format. {name}");
            }
            return genericStart ? throw new FormatException($"Invalid generic argument format. {name}") : false;
        }
        public Type[] ConvertGenericArguments(IReadOnlyDictionary<string, Type>? dic = null)
        {
            if (_generics.Count > 0)
            {
                Type[] genericArgs = new Type[_generics.Count];
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    TypeString tstring = _generics[i];
                    Type realtype = TypeCache.GetTypeOrThrow(tstring.String, dic);
                    if (realtype.IsGenericTypeDefinition) realtype = realtype.MakeGenericType(tstring.ConvertGenericArguments(dic));
                    genericArgs[i] = realtype;
                }
                return genericArgs;
            }
            throw new ArgumentException($"No generic parameters were provided to {GetType().Name} with name value {String} ");
        }
        void LexGenerics()
        {
            GenericString currentGeneric = this;
            StringBuilder sb = new();
            bool finishedReadingLeadName = false;
            for (int i = 0; i < _fullname.Length; i++) //this ones a lot simpler doesnt have smart whitespace skipping and doesnt actually check for context like
            {                                           //whether or not its reading a proper identifier and not 83528474
                char c = _fullname[i];                  //kinda busted it out quickly so it will allow things that the invocationlexer would throw for
                if (c == ' ') continue;                 //like collecting < s t r i n g> into string or allowing impossible names to lex
                if (c == '<')
                {
                    if (finishedReadingLeadName)
                    {
                        TypeString tstring = TypeString.New(sb.ToString(), true);
                        sb.Length = 0;
                        currentGeneric._generics.Add(tstring);
                        tstring._typeArgOf = currentGeneric;
                        currentGeneric = tstring;
                    }
                    else
                    {
                        currentGeneric.String = sb.ToString();
                        sb.Length = 0;
                        finishedReadingLeadName = true;
                    }
                }
                else if (c == '>')
                {
                    if (sb.Length > 0)
                    {
                        string s = sb.ToString();
                        sb.Length = 0;
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        TypeString tstring = TypeString.New(s, true);
                        tstring._typeArgOf = currentGeneric;
                        currentGeneric._generics.Add(tstring);
                        if (currentGeneric is MethodString) break;
                        else
                        {
                            TypeString curr = (TypeString)currentGeneric;
                            if (curr._typeArgOf == null) break;
                            currentGeneric = curr._typeArgOf;
                        }
                    }
                }
                else if (c == ',')
                {
                    if (sb.Length > 0)
                    {
                        string s = sb.ToString();
                        sb.Length = 0;
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        TypeString tstring = TypeString.New(s, true);
                        tstring._typeArgOf = currentGeneric;
                        currentGeneric._generics.Add(tstring);
                    }
                }
                else sb.Append(c);
            }
        }
        public override string ToString()
        {
            return _fullname;
        }

        string PrintGenericArgs()
        {
            StringBuilder sb = new();
            sb.Append(String);
            sb.Append('<');
            sb.AppendMany(_generics, ", ");
            sb.Append('>');
            return sb.ToString();
        }




    }
    internal abstract class GenericString<T> : GenericString where T : MemberInfo
    {
        private protected GenericString(string name, string fullname) : base(name, fullname)
        {
            if (typeof(T) != typeof(Type) && typeof(T) != typeof(MethodInfo)) throw new NotSupportedException($"{typeof(T)}");
        }

        public abstract T ConvertToGeneric(T genericDef, IReadOnlyDictionary<string, Type>? types = null);
    }
    internal sealed class TypeString : GenericString<Type>
    {

        internal GenericString? _typeArgOf; //mostly used for generic lexing, not really necessary to be exposed right now
        //(kind of like paramOf)
        TypeString(string nameForSnipping, string fullNamePreserved) : base(nameForSnipping, fullNamePreserved)
        {

        }
        public override Type ConvertToGeneric(Type genericTypeDef, IReadOnlyDictionary<string, Type>? types = null)
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


        internal static TypeString New(string name, bool fromLex = false)
        {
            name = name.Trim();
            return New<TypeString>(new(name, name), fromLex);
        }

    }
    internal sealed class MethodString : GenericString<MethodInfo>, IMemberString
    {
        public readonly MethodString? ParamOf;
        public TypeString? DeclaringType => _type;
        TypeString? _type;
        public readonly IReadOnlyList<ParameterString> Params;
        List<ParameterString> _params = new();
        internal static MethodString New(string name, MethodString? paramOf, TypeString? declaredIn)
        {
            return New<MethodString>(new(name, name, paramOf, declaredIn));
        }
        MethodString(string nameForSnipping, string fullNamePreserved, MethodString? paramOf, TypeString? type) : base(nameForSnipping, fullNamePreserved)
        {
            ParamOf = paramOf;
            _type = type;
            Params = _params.AsReadOnly();
        }

        public override MethodInfo ConvertToGeneric(MethodInfo genericMethodDef, IReadOnlyDictionary<string, Type>? types = null)
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
        public void AddParameter(ParameterString param) => _params.Add(param);

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
            sb.AppendMany(Params, ", ", x =>
            {
                if (x is MethodString ms)
                {
                    ms.ParamStringShort(sb2);
                    string s = sb2.ToString();
                    sb2.Length = 0;
                    return s;
                }
                else return x!.String;
            });


        }


        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(NameWithGenerics);
            sb.Append($" :: Nested in ");
            ParamOf?.ParamStringShort(sb);
            sb.Append(" :: ");
            sb.Append($"TypeName {DeclaringType?.NameWithGenerics} :: ");
            sb.Append("Params: ");
            if (Params.Count > 0) AppendParams(sb);
            return sb.ToString();

        }
    }
}