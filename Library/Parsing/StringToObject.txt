using System.Reflection;
using System.Text;
using BonesClassLibrary.Collections;
using BonesClassLibrary.Reflection;
using HarmonyLib;

namespace BonesClassLibrary.Parsing;


/// <summary>
/// Reads a string and parses it for names of actual Types, returns them as Type objects.
/// </summary>

public sealed class StringToObject
{
    /// <summary>
    /// Case sensitive, and requires full namespace.
    /// </summary>
    public string TypeName; //can change typename to build a new type 
    public ValueParser ValueParser = new();
    public TypeParser TypeParser = new();

    /// <summary>
    /// Requires namespace.
    /// </summary>
    /// <param name="typeName"></param>

    public StringToObject(string typeName)
    {
        TypeName = typeName;
    }
    //If you are using TypeParser to get type[] parameters from a string, it is paramount that the type names are placed in order, same as methods

    //i want to make a generic version, all it would need is an object array and it could use that to find the right ctor and invoke it with the params
    //but atp you could just actually construct the object normally, this is for stringparsing

    /// <summary>
    /// parameterValues are parsed by ValueParser, which supports primitives and strings. ValueParser requires a TypeMatch to properly parse strings - parameterTypes
    /// is the explicit type names of the values you are inputting, including namespace.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="parameterTypes"></param>
    /// <param name="slice"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public object Construct(string parameterValues, string parameterTypes, char? slice = null)
    {
        Type type = TypeParser.GetType(TypeName);
        TypeParser.Input = parameterTypes;
        TypeParser.Slice = slice;
        Type[] types = TypeParser.GetParsedArray();
        return ConstructInternal(type, parameterValues, slice, types!);
    }

    object ConstructInternal(Type type, string parameters, char? slice = null, params Type[] types)
    {
        ConstructorInfo? ctor;
        try
        {
            ctor = type.GetConstructor(types);
        }
        catch (MissingMemberException ex)
        {
            throw new ArgumentException($"{Exception(types)} {ex}");
        }
        ValueParser.Input = parameters;
        ValueParser.UpdateParameters(ctor);
        ValueParser.Slice = slice;
        object?[] objects = ValueParser.GetParsedArray();
        return ctor.Invoke(objects);
    }

    static string Exception(Type[] types)
    {

        if (types.Length == 0)
            return "Could not find public parameterless constructor.";
        StringBuilder sb = new();
        sb.Append("Could not find constructor with parameters of type ");
        for (int i = 0; i < types.Length; i++)
        {
            sb.Append($"{types[i]}");
            if (types.Length > 1 && i < types.Length - 1)
                sb.Append($", ");
        }
        return sb.ToString();
    }
}