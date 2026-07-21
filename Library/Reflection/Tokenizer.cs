using System.Reflection;
using System.Collections;

namespace XQuinn.Reflection.Tokenizer;

public class BaseObject
{
    public readonly Token Token;

    public readonly object? Instance;

    public BaseObject(Token token, object? Object)
    {
        this.Token = token;
        this.Instance = Object;
    }

    public bool IsNull => Token == Token.Null;

    public bool IsReferenceType => Token == Token.ClassOrStruct;

    public bool IsCollection => Token == Token.IList || Token == Token.IDictionary;

    public bool IsSimple
    => Token switch
    {
        Token.PrimitiveStruct or Token.String or Token.Enum or Token.Delegate or Token.Boolean => true,
        _ => false
    };

    public static Token GetToken(object? obj)
     =>
        obj switch
        {
            null => Token.Null,
            string => Token.String,
            bool => Token.Boolean,
            Enum => Token.Enum,
            ValueType => EvaluateValueType(obj),
            IDictionary => Token.IDictionary,
            IList => Token.IList,
            Delegate => Token.Delegate, //this may require extra work, dont know how to read the delegate value yet
            _ => Token.ClassOrStruct
        };
    static Token EvaluateValueType(object obj)
    {
        Type type = obj.GetType();
        if (type.IsPrimitive)
            return Token.PrimitiveStruct;
        else
            return Token.ClassOrStruct;
    }

}



public class Element : BaseObject
{
    public Element(Token token, object? Object) : base(token, Object)
    {
    }
}

public class Field : BaseObject
{
    public readonly FieldInfo FieldInfo;

    public readonly Type SourceType; //the type of the instance that the field's value "exists" in (debatable term if the field is private and was declared in a base class)
                                     //that being said SourceType obviously may be different from the field's actual declaring type 
    public Field(Token token, FieldInfo field, object Object, Type sourceType) : base(token, Object)
    {
        FieldInfo = field;
        SourceType = sourceType;
    }
}

public enum Token  //tokens are used to figure out what means we will use to read the value
{
    _invalid, //unused default value
    Null,
    IList, //IList and ICollection can probably be grouped together but we need to check if its an IDictionary first then
    IDictionary, //need to add ICollection support for hashish tables
    PrimitiveStruct, 
    String, //strings are classes read like primitive structs
    ClassOrStruct, //Classes and nonprimitive structs (called "Custom Types" by the reader)
    Enum,
    Delegate,
    Boolean //booltostring is converted to its lower variant
}