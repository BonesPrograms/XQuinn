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

namespace XQuinn.CodeAnalysis.AST
{

    public interface IMemberString
    {
        public TypeString? DeclaringType { get; }
    }
    //"abstract syntax tree"

    //This is tightly coupled to CallInterpreter. It can be used elsewhere (I use it in RuntimeCommands) but actually turning parameters to objects or generic parameters to type arrays
    //relies on methods in CallInterpreter. I may decouple it later, but I found it difficult - CallInterpreter.GetGenerics() relies on CallInterpreter.
    // FindType(), and I think it is just weird to encapsulate
    //FindType(TypeString) behind GenericParameter, it should be encapsulated as part of the TypeString class, but GenericParameter would need access to that method, its just a mess rn lol
    //Parameter doesnt support Generics so you cannot just move it all up to the top class
    public abstract class ParameterString
    {
        public string String { get => _string; protected set => _string = value; }
        public readonly MethodString? ParamOf;
        string _string;
        public ParameterString(string String, MethodString? paramof)
        {
            _string = String;
            ParamOf = paramof;
        }
        public override string ToString()
        {
            return String + $" Param Of: {ParamOf}";
        }


    }

    public sealed class ValueString : ParameterString
    {
        public ValueString(string String, MethodString? paramOf) : base(String, paramOf)
        {

        }
        ///This does not work with FieldString,MethodString or TypeString. it is for true ParameterStrings
        public object? ParseValue(Type asType)
        {
            if (String.EqualsCaseless("default")) return asType.GetDefaultValue();
            if (asType.IsGenericType && asType.GetGenericTypeDefinition() == typeof(Nullable<>)) //ValueType of Nullable<T>
            {
                if (String.EqualsCaseless("null")) return null;
                asType = Nullable.GetUnderlyingType(asType) ?? throw new ArgumentException($"Underlying type for Nullable<T> type {asType} is null.");
            }
            if (asType.IsClass) { if (String.EqualsCaseless("null")) return null; else return asType == typeof(string) ? RegexString() : throw new NotSupportedException($"Cannot convert values to reference type instances. class type: {asType} Value: {String}"); }
            if (asType.IsValueType)
            {
                if (asType.IsEnum) { if (EnumNet20.TryParse(String, asType, true, out Enum? @enum)) return @enum; }
                else if (asType.IsPrimitive) { if (ParsePrimitive(asType, out ValueType? primitive)) return primitive; }
                else throw new NotSupportedException($"Cannot convert values to user defined struct instances. struct type: {asType}. Value: {String}");
            }
            throw new FormatException($"Failed to parse {String} to {asType}.");
        }

        string RegexString()
        {
            const string regex = "^\"(.*?)\"$";// "\"([^\"]*)\""; //this new one supports fuckery like this "hello "world"" so itll pop out as hello "world" :)
            var matches = Regex.Match(String, regex);
            return matches.Success ? matches.Groups[1].Value : throw new FormatException($"Non-null string values must be surrounded with quotations, even empty strings. Bad input: {String}");
        }
        bool ParsePrimitive(Type asType, out ValueType? primitive) //Almost looks like a switch!
        {
            primitive = null;
            if (asType == typeof(bool)) { if (bool.TryParse(String, out bool boolean)) primitive = boolean; }
            else if (asType == typeof(char)) { if (char.TryParse(String, out char utf16)) primitive = utf16; }
            else if (asType == typeof(byte)) { if (byte.TryParse(String, out byte uint8)) primitive = uint8; }
            else if (asType == typeof(sbyte)) { if (sbyte.TryParse(String, out sbyte sint8)) primitive = sint8; }
            else if (asType == typeof(short)) { if (short.TryParse(String, out short sint16)) primitive = sint16; }
            else if (asType == typeof(ushort)) { if (ushort.TryParse(String, out ushort uint16)) primitive = uint16; }
            else if (asType == typeof(int)) { if (int.TryParse(String, out int sint32)) primitive = sint32; }
            else if (asType == typeof(uint)) { if (uint.TryParse(String, out uint uint32)) primitive = uint32; }
            else if (asType == typeof(long)) { if (long.TryParse(String, out long sint64)) primitive = sint64; }
            else if (asType == typeof(ulong)) { if (ulong.TryParse(String, out ulong uint64)) primitive = uint64; }
            else if (asType == typeof(float)) { if (float.TryParse(String, out float float32)) primitive = float32; }
            else if (asType == typeof(double)) { if (double.TryParse(String, out double float64)) primitive = float64; }
            else if (asType == typeof(decimal)) { if (decimal.TryParse(String, out decimal dec)) primitive = dec; }
            else if (asType == typeof(nint)) { if (NIntNet20.TryParse(String, out nint nativesint)) primitive = nativesint; }
            else if (asType == typeof(nuint)) { if (NUIntNet20.TryParse(String, out nuint nativeuint)) primitive = nativeuint; }
            return primitive != null;
        }

    }

    public sealed class FieldString : ParameterString, IMemberString
    {
        public TypeString DeclaringType => _type;
        TypeString _type;
        public FieldString(string name, MethodString? paramOf, TypeString declaredIn) : base(name.Trim(), paramOf)
        {
            _type = declaredIn;
        }

    }

    public abstract class GenericString : ParameterString
    {
        public string NameWithGenerics => _fullname;
        string _fullname;
        public IReadOnlyList<TypeString>? Generics => _generics;
        IReadOnlyList<TypeString>? _generics;
        protected GenericString(string nameForSnipping, string fullNamePreserved, MethodString? paramOf) : base(nameForSnipping, paramOf)
        {
            _fullname = fullNamePreserved;
        }
        protected static T New<T>(T genericString) where T : GenericString
        {
            genericString.SnipGenericsOff();
            return genericString;
        }
        public Type[] ConvertGenericArguments(IReadOnlyDictionary<string, Type>? dic = null)
        {
            if (_generics != null)
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

        void SnipGenericsOff()
        {
            const string regex = "^\\s*[^<]*<(?<inner>(?:[^<>]+|<(?<depth>)|>(?<-depth>))*)(?(depth)(?!))>\\s*$"; //"^[^<]*<(.+)>$";
            var matches = Regex.Match(String, regex);
            if (matches.Success)
            {
                string[] args = matches.Groups[1].Value.Split(','
#if NET6_0_OR_GREATER
                , StringSplitOptions.TrimEntries
#endif
                );
                _generics = args.Select(x => TypeString.New(x, null)).ToList().AsReadOnly();
                // int genericEnd = String.IndexOf('>');
                //for (int x = genericEnd + 1; x < String.Length; x++) if (String[x] != ' ') throw new LexicalException($"Detected trailing characters after generic input. ", String);
                String = String.Remove(String.IndexOf('<'));
                if (_fullname.Any(x => x == ' '))
                {
                    StringBuilder sb = new();
                    sb.Append(String);
                    sb.Append('<'); //we do it like this because of potential whitespace variations - method<string,  int> or method<string,int> would be treated differently if not
                    for (int z = 0; z < _generics.Count; z++) { sb.Append(_generics[z].String); if (For.Multiples(_generics.Count, z)) sb.Append(", "); }
                    sb.Append('>');
                    _fullname = sb.ToString();
                }
            }
            //             else
            //             {
            //                 string[] args = String.Split(','
            // #if NET6_0_OR_GREATER
            // , StringSplitOptions.TrimEntries
            // #endif
            // );
            //                 _generics = args.Select(x => TypeString.New(x, null)).ToList().AsReadOnly();
            //                 int end = String.IndexOf('<');
            //                 if (end == -1)
            //                     end = String.IndexOf('>');
            //                 String = String.Remove(end);
            //             }

        }
    }
    public abstract class GenericString<T> : GenericString where T : MemberInfo
    {
        protected GenericString(string name, string fullname, MethodString? paramOf) : base(name, fullname, paramOf)
        {
            if (typeof(T) != typeof(Type) && typeof(T) != typeof(MethodInfo)) throw new NotSupportedException($"{typeof(T)}");
        }

        public abstract T ConvertToGeneric(T genericDef, IReadOnlyDictionary<string, Type>? types = null);
    }
    public sealed class TypeString : GenericString<Type>
    {
        TypeString(string nameForSnipping, string fullNamePreserved, MethodString? paramOf) : base(nameForSnipping, fullNamePreserved, paramOf)
        {

        }
        public override Type ConvertToGeneric(Type genericTypeDef, IReadOnlyDictionary<string, Type>? types = null)
        {
            return genericTypeDef.MakeGenericType(ConvertGenericArguments(types));
        }

        public static TypeString New(string name, MethodString? paramOf)
        {
            return New<TypeString>(new(name.Trim(), name, paramOf));
        }

    }
    public sealed class MethodString : GenericString<MethodInfo>, IMemberString
    {

        public TypeString? DeclaringType => _type;
        TypeString? _type;
        public readonly IReadOnlyList<ParameterString> Params;
        List<ParameterString> _params = new();
        public static MethodString New(string name, MethodString? paramOf, TypeString? declaredIn)
        {
            return New<MethodString>(new(name, name, paramOf, declaredIn));
        }
        MethodString(string nameForSnipping, string fullNamePreserved, MethodString? paramOf, TypeString? type) : base(nameForSnipping, fullNamePreserved, paramOf)
        {
            _type = type;
            Params = _params.AsReadOnly();
        }

        public override MethodInfo ConvertToGeneric(MethodInfo genericMethodDef, IReadOnlyDictionary<string, Type>? types = null)
        {
            return genericMethodDef.MakeGenericMethod(ConvertGenericArguments(types));
        }
        public void AddParameter(ParameterString param) => _params.Add(param);

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(String);
            sb.Append($" :: Nested in {ParamOf} :: ");
            sb.Append($"TypeName {DeclaringType?.String} :: ");
            sb.Append("Params: ");
            for (int i = 0; i < Params.Count; i++) { sb.Append(Params[i].String); if (For.Multiples(Params.Count, i)) sb.Append(", "); }
            return sb.ToString();

        }
    }
}