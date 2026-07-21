using System.Text.RegularExpressions;
using System.Text;

namespace XQuinn.Parsing.AST;
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
        protected set
        {
            _string = value;
        }
    }
    string _string;
    public readonly Method? _paramOf;
    public string? ParamOf => _paramOf?.String;

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
    public Field(string name, Method? paramOf, TypeString type) : base(name,paramOf)
    {
        _type = type;
    }
    
}

public abstract class GenericParameter : Parameter
{
    public IReadOnlyList<TypeString>? Generics //Chicken and egg problem: TypeStrings came first. Eggs make the chicken
    {
        get => _generics;
        private set
        {
            _generics = value;
        }
    }

    IReadOnlyList<TypeString>? _generics;

    public bool IsGeneric => Generics == null;

    public GenericParameter(string name, Method? paramOf) : base(name, paramOf)
    {
        GenericNames(name);
    }

    public GenericParameter(string name) : this(name, null)
    {
    }

    void GenericNames(string name)
    {
        const string regex = "<([^>]+)>";
        var matches = Regex.Match(name, regex);
        if (matches.Success)
        {
            string[] args = matches.Groups[1].Value.Split(',');
            Generics = args.Select(x => new TypeString(x, null)).ToList().AsReadOnly();
            String = String[..String.IndexOf('<')];
        }

    }
}
public class TypeString : GenericParameter
{
    //ParamOf == null usually means this is a type parameter
    public TypeString(string name, Method? paramof) : base(name, paramof)
    {
    }

    public TypeString(string name) : base(name, null)
    {

    }

}
public class Method : GenericParameter
{
    public string? DeclaringType => _type?.String;
    public readonly TypeString? _type;
    public readonly IReadOnlyList<Parameter> Params;
    List<Parameter> _params = [];
    public Method(string name, Method? NestedIn, TypeString typeName) : this(name, NestedIn)
    {
        _type = typeName;
    }

    public Method(string name, Method? NestedIn) : base(name, NestedIn)
    {
        Params = _params.AsReadOnly();
    }
    public Method(string name) : this(name, null)
    {

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
