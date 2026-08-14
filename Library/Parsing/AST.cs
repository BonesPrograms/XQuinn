using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using XQuinn.Reflection;
using HarmonyLib;

namespace XQuinn.Parsing.AST
{

    public interface IMember
    {
        public TypeString? DeclaringType {get;}
    }
    //"abstract syntax tree"

    //This is tightly coupled to CallInterpreter. It can be used elsewhere (I use it in RuntimeCommands) but actually turning parameters to objects or generic parameters to type arrays
    //relies on methods in CallInterpreter. I may decouple it later, but I found it difficult - CallInterpreter.GetGenerics() relies on CallInterpreter.
    // FindType(), and I think it is just weird to encapsulate
    //FindType(TypeString) behind GenericParameter, it should be encapsulated as part of the TypeString class, but GenericParameter would need access to that method, its just a mess rn lol
    //Parameter doesnt support Generics so you cannot just move it all up to the top class
    public class ParameterString
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
        readonly bool _isParemterString;
        public ParameterString(string String, MethodString? paramof)
        {
            _string = String;
            ParamOf = paramof;
            _isParemterString = GetType() == typeof(ParameterString);
        }


        public override string ToString()
        {
            return String + $" Param Of: {ParamOf}";
        }

        ///This does not work with FieldString,MethodString or TypeString. it is for true ParameterStrings
        public object? ParseParameter(Type type)
        {
            if (!_isParemterString)
                throw new NotSupportedException("ParseParameter does not function properly for MethodString,TypeString and FieldString.");
            string strng = String;
            if (strng == "default")
                return type.GetDefaultValue();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) //ValueType of Nullable<T>
            {
                if (strng == "null")
                    return null;
                Type? underlying = Nullable.GetUnderlyingType(type) ?? throw new ArgumentException($"Underlying type for Nullable<T> type {type} is null.");
            }
            if (type.IsClass && (strng == "null" || type != typeof(string)))
                return null;
            if (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
                return null; //user defined struct
            if (type.IsEnum)
            {
                object? parsed = null;
                try
                {
                    parsed = Enum.Parse(type, strng, true);
                }
                catch (ArgumentException)
                {

                }
                if (parsed != null)
                    return parsed;
            }
            if (type == typeof(string))//&& strng.Length >= 2 && strng[0] == StringDeclr && strng[^1] == StringDeclr)
            {
                const string regex = "^\"(.*?)\"$";// "\"([^\"]*)\""; //this new one supports fuckery like this "hello "world"" so itll pop out as hello "world" :)
                var matches = Regex.Match(strng, regex);
                if (matches.Success)
                    return matches.Groups[1].Value;
                else
                    throw new FormatException($"Non-null string values must be surrounded with quotations, even empty strings. Bad input: {strng}");
            }
            if ((type == typeof(bool)) && bool.TryParse(strng, out bool boolean))
                return boolean;
            if ((type == typeof(char)) && char.TryParse(strng, out char utf16))
                return utf16;
            if ((type == typeof(byte)) && byte.TryParse(strng, out byte uint8))
                return uint8;
            if ((type == typeof(sbyte)) && sbyte.TryParse(strng, out sbyte sint8))
                return sint8;
            if ((type == typeof(short)) && short.TryParse(strng, out short sint16))
                return sint16;
            if ((type == typeof(ushort)) && ushort.TryParse(strng, out ushort uint16))
                return uint16;
            if ((type == typeof(int)) && int.TryParse(strng, out int sint32))
                return sint32;
            if ((type == typeof(uint)) && uint.TryParse(strng, out uint uint32))
                return uint32;
            if ((type == typeof(long)) && long.TryParse(strng, out long sint64))
                return sint64;
            if ((type == typeof(ulong)) && ulong.TryParse(strng, out ulong uint64))
                return uint64;
            if ((type == typeof(float)) && float.TryParse(strng, out float float32))
                return float32;
            if ((type == typeof(double)) && double.TryParse(strng, out double float64))
                return float64;
            if ((type == typeof(decimal)) && decimal.TryParse(strng, out decimal dec))
                return dec;
#if NET6_0_OR_GREATER
            if (type == typeof(nint) && nint.TryParse(strng, out nint nativeint))
                return nativeint;
            if (type == typeof(nuint) && nuint.TryParse(strng, out nuint nativeuint))
                return nativeuint;
#else
            if (type == typeof(nint) || type == typeof(uint)) //i never use nint or uint so i havent fixed this yet but i will later maybe lol
                throw new NotSupportedException("Parsing nint and uint not yet supported for pre-net6.");
#endif
            throw new FormatException($"Tried to parse {strng} to {type}, but value could not parse to {type}.");
        }
    }

    public sealed class FieldString : ParameterString, IMember
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
        public IReadOnlyList<TypeString>? Generics => _generics;
        IReadOnlyList<TypeString>? _generics;
        protected GenericString(string name, MethodString? paramOf) : base(name, paramOf)
        {

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
                _generics = args.Select(x => TypeString.New(x.Trim(), null)).ToList().AsReadOnly();
                int i = String.IndexOf('>');
                for (int x = i + 1; x < String.Length; x++)
                {
                    if (String[x] != ' ')
                    {
                        throw new LexicalException($"Detected trailing characters after generic input. ", String);
                    }
                }
                String = String.Remove(String.IndexOf('<'));
            }

        }
    }
    public sealed class TypeString : GenericString
    {
        TypeString(string name, MethodString? paramOf) : base(name, paramOf)
        {

        }

        public Type ConvertToGenericType(Type genericTypeDefinition, IReadOnlyDictionary<string, Type>? types = null)
        {
            Type[] generics = ConvertGenericArguments(types);
            return genericTypeDefinition.MakeGenericType(generics);

        }

        public static TypeString New(string name, MethodString? paramOf)
        {
            return New<TypeString>(new(name.Trim(), paramOf));
        }

    }
    public sealed class MethodString : GenericString, IMember
    {

        public TypeString? DeclaringType => _type;
        TypeString? _type;
        public readonly IReadOnlyList<ParameterString> Params;
        List<ParameterString> _params = new();
        public static MethodString New(string name, MethodString? paramOf, TypeString? type)
        {
            return New<MethodString>(new(name, paramOf, type));
        }
        MethodString(string name, MethodString? paramOf, TypeString? type) : base(name, paramOf)
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
                if (Params.Count > 1 && i < Params.Count - 1)
                    sb.Append(", ");
            }
            return sb.ToString();

        }
    }
}