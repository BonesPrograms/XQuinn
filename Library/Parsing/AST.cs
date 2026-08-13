using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using XQuinn.Reflection;

namespace XQuinn.Parsing.AST
{
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
        public MethodString? _paramOf
        {
            get => _paramOfInternal;
            protected set => _paramOfInternal = value;
        }
        string _string = null!;
        MethodString? _paramOfInternal;
        public string? ParamOf => _paramOf?.String;

        protected ParameterString()
        {

        }
        public ParameterString(string name, MethodString? paramof) : this(name)
        {
            _paramOf = paramof;
        }
        public ParameterString(string name)
        {
            _string = name;
        }

        public override string ToString()
        {
            return String + $" Param Of: {ParamOf}";
        }

        public object? ParseParameter(Type type) //need half and int128 support
        {
            string strng = String;
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

    public class FieldString : ParameterString
    {
        public string DeclaringType => _type.String;
        public TypeString _type;

        public FieldString(string name, TypeString type) : base(name)
        {
            _type = type;
        }
        public FieldString(string name, MethodString? paramOf, TypeString type) : base(name, paramOf)
        {
            _type = type;
        }

    }

    public abstract class GenericString : ParameterString
    {
        public IReadOnlyList<TypeString>? Generics => _generics;
        IReadOnlyList<TypeString>? _generics;

        public T ConstructGeneric<T>(T obj, Func<TypeString, Type>? getTypeByString = null) where T : MemberInfo
        {
            Type[] generics = GetGenerics(getTypeByString);
            if (obj is Type type)
            {
                if (this is TypeString)
                    return (T)(object)type.MakeGenericType(generics);
                else
                    throw new ArgumentException("Attempting to construct generic Type from Method AST.");
            }
            else if (obj is MethodInfo method)
            {
                if (this is MethodString)
                    return (T)(object)method.MakeGenericMethod(generics);
                else
                    throw new ArgumentException("Attempting to construct generic method from TypeString AST.");
            }
            else
                throw new NotSupportedException("Can only construct generics from type and method definitions.");
        }

        ///Gets generic string arguments from a Method AST object.
        public Type[] GetGenerics(Func<TypeString, Type>? func)
        {
            if (_generics != null)
            {
                Type[] arr = new Type[_generics.Count];
                for (int i = 0; i < arr.Length; i++)
                    if (func != null)
                        arr[i] = func.Invoke(_generics[i]);
                    else
                        arr[i] = TypeCache.GetTypeOrThrow(_generics[i].String);
                return arr;
            }
            throw new ArgumentException($"No generic parameters were provided to {GetType().Name} with name value {String} ");
        }
        protected GenericString()
        {

        }
        protected static T New<T>(string name, MethodString? paramOf) where T : GenericString
        {
            T genericParameter = (T)Activator.CreateInstance(typeof(T), true)!;
            genericParameter.String = name.Trim();
            genericParameter._paramOf = paramOf;
            genericParameter.GenericNames(name);
            return genericParameter;
        }

        void GenericNames(string name)
        {
            const string regex = "<([^>]+)>";
            var matches = Regex.Match(name, regex);
            if (matches.Success)
            {
                string[] args = matches.Groups[1].Value.Split(',');
                _generics = args.Select(x => TypeString.New(x, null)).ToList().AsReadOnly();
                int i = String.IndexOf('>');
                for (int x = i + 1; x < String.Length; x++)
                {
                    if (String[x] != ' ')
                    {
                        throw new LexicalException($"Detected trailing characters after generic input. ", name);
                    }
                }
                String = String.Remove(String.IndexOf('<'));
            }

        }
    }
    public class TypeString : GenericString
    {
        TypeString()
        {

        }

        public static TypeString New(string name, MethodString? paramOf)
        {
            return New<TypeString>(name, paramOf);
        }

    }
    public class MethodString : GenericString
    {
        MethodString()
        {
            Params = _params.AsReadOnly();
        }
        public string? DeclaringType => _type?.String;
        public TypeString? _type => _typeInternal;
        TypeString? _typeInternal;
        public readonly IReadOnlyList<ParameterString> Params;
        List<ParameterString> _params = new();
        public static MethodString New(string name, MethodString? NestedIn, TypeString? typeName)
        {
            MethodString method = New<MethodString>(name, NestedIn);
            method._typeInternal = typeName;
            return method;
        }

        public void Add(ParameterString param)
        {
            _params.Add(param);
        }
        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(String);
            sb.Append($" :: Nested in {ParamOf} :: ");
            sb.Append($"TypeName {DeclaringType} :: ");
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