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
        public string String //For a standard parameter this would be a primitive value or string literal, for typestrings it is a typename, for methods it is the method name.
        {
            get => _string;
            protected set => _string = value;

        }
        public MethodString? ParamOf
        {
            get => _paramOf;
            protected set => _paramOf = value;
        }
        string _string;
        MethodString? _paramOf;
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
            if (String == "default")
                return asType.GetDefaultValue();
            if (asType.IsGenericType && asType.GetGenericTypeDefinition() == typeof(Nullable<>)) //ValueType of Nullable<T>
            {
                if (String == "null")
                    return null;
                asType = Nullable.GetUnderlyingType(asType) ?? throw new ArgumentException($"Underlying type for Nullable<T> type {asType} is null.");
            }
            if (asType.IsClass)
                return ParseClass(asType);
            if (asType.IsValueType)
            {
                if (asType.IsEnum)
                {
                    if (EnumNet20.TryParse(String, asType, true, out Enum? @enum))
                        return @enum;
                }
                else if (asType.IsPrimitive)
                {
                    if (ParsePrimitive(asType, out ValueType? primitive))
                        return primitive;
                }
                else
                    throw new NotSupportedException($"Cannot parse values to user defined structs. struct type: {asType}");
            }
            throw new FormatException($"Failed to parse {String} to {asType}.");
        }

        string? ParseClass(Type asType)
        {
            if (String == "null")
                return null;
            return asType == typeof(string) ? RegexString() : throw new NotSupportedException($"Cannot parse values to class instances. class type: {asType}");
        }

        string RegexString()
        {
            const string regex = "^\"(.*?)\"$";// "\"([^\"]*)\""; //this new one supports fuckery like this "hello "world"" so itll pop out as hello "world" :)
            var matches = Regex.Match(String, regex);
            return matches.Success ? matches.Groups[1].Value : throw new FormatException($"Non-null string values must be surrounded with quotations, even empty strings. Bad input: {String}");
        }
        bool ParsePrimitive(Type asType, out ValueType? primitive)
        {
            primitive = null;
            if (asType == typeof(bool))
            {
                if (bool.TryParse(String, out bool boolean))
                    primitive = boolean;
            }
            else if (asType == typeof(char))
            {
                if (char.TryParse(String, out char utf16))
                    primitive = utf16;
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
            else if (asType == typeof(int))
            {
                if (int.TryParse(String, out int sint32))
                    primitive = sint32;
            }
            else if (asType == typeof(uint))
            {
                if (uint.TryParse(String, out uint uint32))
                    primitive = uint32;
            }
            else if (asType == typeof(long))
            {
                if (long.TryParse(String, out long sint64))
                    primitive = sint64;
            }
            else if (asType == typeof(ulong))
            {
                if (ulong.TryParse(String, out ulong uint64))
                    primitive = uint64;
            }
            else if (asType == typeof(float))
            {
                if (float.TryParse(String, out float float32))
                    primitive = float32;
            }
            else if (asType == typeof(double))
            {
                if (double.TryParse(String, out double float64))
                    primitive = float64;
            }
            else if (asType == typeof(decimal)) //not a primitive i heard but we support this
            {
                if (decimal.TryParse(String, out decimal dec))
                    primitive = dec;
            }
            else if (asType == typeof(nint))
            {
                if (NIntNet20.TryParse(String, out nint nativesint))
                    primitive = nativesint;
            }
            else if (asType == typeof(nuint))
            {
                if (NUIntNet20.TryParse(String, out nuint nativeuint))
                    primitive = nativeuint;
            }
            return primitive != null;
        }

    }

    public sealed class FieldString : ParameterString, IMemberString
    {
        public TypeString DeclaringType => _type;
        TypeString _type;
        public FieldString(string name, MethodString? paramOf, TypeString declaringType) : base(name, paramOf)
        {
            _type = declaringType;
        }

    }

    public abstract class GenericString : ParameterString
    {
        public string NameWithGenerics => _fullname;
        string _fullname;
        public IReadOnlyList<TypeString>? Generics => _generics;
        IReadOnlyList<TypeString>? _generics;
        protected GenericString(string name, string fullname, MethodString? paramOf) : base(name, paramOf)
        {
            _fullname = fullname;
        }
        protected static T New<T>(T genericString) where T : GenericString
        {
            genericString.GenericNames();
            return genericString;
        }
        public Type[] ConvertGenericArguments(IReadOnlyDictionary<string, Type>? dic)
        {
            if (_generics != null)
            {
                Type[] genericArgs = new Type[_generics.Count];
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    TypeString tstring = _generics[i];
                    Type t = TypeCache.GetTypeOrThrow(tstring.String, dic);
                    if (t.IsGenericTypeDefinition)
                    {
                        Type[] subGenericArgs = tstring.ConvertGenericArguments(dic);
                        t = t.MakeGenericType(subGenericArgs);
                    }
                    genericArgs[i] = t;
                }
                return genericArgs;
            }
            throw new ArgumentException($"No generic parameters were provided to {GetType().Name} with name value {String} ");
        }

        void GenericNames()
        {
            const string regex = "<([^>]+)>";
            var matches = Regex.Match(String, regex);
            if (matches.Success)
            {
                string[] args = matches.Groups[1].Value.Split(',');
                _generics = args.Select(x => TypeString.New(x, null)).ToList().AsReadOnly();
                int genericEnd = String.IndexOf('>');
                for (int x = genericEnd + 1; x < String.Length; x++)
                {
                    if (String[x] != ' ')
                    {
                        throw new LexicalException($"Detected trailing characters after generic input. ", String);
                    }
                }
                String = String.Remove(String.IndexOf('<'));
                StringBuilder sb = new();
                sb.Append(String);
                sb.Append('<'); //we do it like this because of potential whitespace variations - method<string,  int> or method<string,int> would be treated differently if not
                for (int z = 0; z < _generics.Count; z++)
                {
                    sb.Append(_generics[z].String);
                    if (For.Multiples(_generics.Count, z))
                        sb.Append(", ");
                }
                sb.Append('>');
                _fullname = sb.ToString();
            }

        }
    }
    public sealed class TypeString : GenericString
    {
        TypeString(string name, string fullname, MethodString? paramOf) : base(name, fullname, paramOf)
        {

        }

        public Type ConvertToGenericType(Type genericTypeDefinition, IReadOnlyDictionary<string, Type>? types = null)
        {
            Type[] generics = ConvertGenericArguments(types);
            return genericTypeDefinition.MakeGenericType(generics);

        }

        public static TypeString New(string name, MethodString? paramOf)
        {
            return New<TypeString>(new(name.Trim(), name, paramOf));
        }

    }
    public sealed class MethodString : GenericString, IMemberString
    {

        public TypeString? DeclaringType => _type;
        TypeString? _type;
        public readonly IReadOnlyList<ParameterString> Params;
        List<ParameterString> _params = new();
        public static MethodString New(string name, MethodString? paramOf, TypeString? type)
        {
            return New<MethodString>(new(name, name, paramOf, type));
        }
        MethodString(string name, string fullname, MethodString? paramOf, TypeString? type) : base(name, fullname, paramOf)
        {
            _type = type;
            Params = _params.AsReadOnly();
        }

        public MethodInfo ConvertToGenericMethod(MethodInfo genericMethodDefinition, IReadOnlyDictionary<string, Type>? types = null)
        {
            Type[] generics = ConvertGenericArguments(types);
            return genericMethodDefinition.MakeGenericMethod(generics);
        }
        public void AddParameter(ParameterString param)
        {
            _params.Add(param);
        }
        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(String);
            sb.Append($" :: Nested in {ParamOf} :: ");
            sb.Append($"TypeName {DeclaringType?.String} :: ");
            sb.Append("Params: ");
            for (int i = 0; i < Params.Count; i++)
            {
                var param = Params[i];
                sb.Append(param.String);
                if (For.Multiples(Params.Count, i))
                    sb.Append(", ");
            }
            return sb.ToString();

        }
    }
}