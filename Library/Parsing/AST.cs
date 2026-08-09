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
    public class Parameter
    {
        public string String //For a standard parameter this would be a primitive value or string literal, for typestrings it is a typename, for methods it is the method name.
        {
            get => _string;
            protected set => _string = value;

        }
        public Method? _paramOf
        {
            get => _paramOfInternal;
            protected set => _paramOfInternal = value;
        }
        string _string = null!;
        Method? _paramOfInternal;
        public string? ParamOf => _paramOf?.String;

        protected Parameter()
        {

        }
        public Parameter(string name, Method? paramof) : this(name)
        {
            _paramOf = paramof;
        }
        public Parameter(string name)
        {
            _string = name;
        }

        public override string ToString()
        {
            return String + $" Param Of: {ParamOf}";
        }
    }

    public class Field : Parameter
    {
        public string DeclaringType => _type.String;
        public TypeString _type;

        public Field(string name, TypeString type) : base(name)
        {
            _type = type;
        }
        public Field(string name, Method? paramOf, TypeString type) : base(name, paramOf)
        {
            _type = type;
        }

    }

    public abstract class GenericParameter : Parameter
    {
        public IReadOnlyList<TypeString>? Generics => _generics;
        IReadOnlyList<TypeString>? _generics;

        public T ConstructGeneric<T>(T obj, Func<string, Type>? getTypeByString = null) where T : MemberInfo
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
                if (this is Method)
                    return (T)(object)method.MakeGenericMethod(generics);
                else
                    throw new ArgumentException("Attempting to construct generic method from TypeString AST.");
            }
            else
                throw new NotSupportedException("Can only construct generics from type and method definitions.");
        }

        ///Gets generic string arguments from a Method AST object.
        public Type[] GetGenerics(Func<string, Type>? func)
        {
            if (_generics != null)
            {
                Type[] arr = new Type[_generics.Count];
                for (int i = 0; i < arr.Length; i++)
                    if (func != null)
                        arr[i] = func.Invoke(_generics[i].String);
                    else
                        arr[i] = TypeCache.GetTypeOrThrow(_generics[i].String);
                return arr;
            }
            throw new ArgumentException($"No generic parameters were provided to {GetType().Name} with name value {String} ");
        }
        protected GenericParameter()
        {

        }
        protected static T New<T>(string name, Method? paramOf) where T : GenericParameter
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
    public class TypeString : GenericParameter
    {
        TypeString()
        {

        }

        public static TypeString New(string name, Method? paramOf)
        {
            return New<TypeString>(name, paramOf);
        }

    }
    public class Method : GenericParameter
    {
        Method()
        {
            Params = _params.AsReadOnly();
        }
        public string? DeclaringType => _type?.String;
        public TypeString? _type => _typeInternal;
        TypeString? _typeInternal;
        public readonly IReadOnlyList<Parameter> Params;
        List<Parameter> _params = new();
        public static Method New(string name, Method? NestedIn, TypeString? typeName)
        {
            Method method = New<Method>(name, NestedIn);
            method._typeInternal = typeName;
            return method;
        }

        public void Add(Parameter param)
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