using System.Reflection;
using System.Collections;
using System;

namespace XQuinn.ObjectModel.Tokenizer
{

public abstract class TokenizedObject
{
    public readonly ObjectToken Token;

    public readonly object? Instance;

    public TokenizedObject(ObjectToken token, object? Object)
    {
        this.Token = token;
        this.Instance = Object;
    }

    public bool IsNull => Token == ObjectToken.Null;

    public bool IsReferenceType => Token == ObjectToken.ClassOrStruct;

    public bool IsCollection => Token == ObjectToken.IList || Token == ObjectToken.IDictionary;

    public bool IsSimple
    => Token switch
    {
        ObjectToken.PrimitiveStruct or ObjectToken.String or ObjectToken.Enum or ObjectToken.Delegate or ObjectToken.Boolean => true,
        _ => false
    };

    public static ObjectToken GetToken(object? obj)
     =>
        obj switch
        {
            null => ObjectToken.Null,
            string => ObjectToken.String,
            bool => ObjectToken.Boolean,
            Enum => ObjectToken.Enum,
            ValueType => EvaluateValueType(obj),
            IDictionary => ObjectToken.IDictionary,
            IList => ObjectToken.IList,
            Delegate => ObjectToken.Delegate, //this may require extra work, dont know how to read the delegate value yet
            _ => ObjectToken.ClassOrStruct
        };
    static ObjectToken EvaluateValueType(object obj)
    {
        Type type = obj.GetType();
        if (type.IsPrimitive)
            return ObjectToken.PrimitiveStruct;
        else
            return ObjectToken.ClassOrStruct;
    }

}



public class ElementObject : TokenizedObject
{
    public ElementObject(ObjectToken token, object? Object) : base(token, Object)
    {
    }
}

public class FieldObject : TokenizedObject
{
    public readonly FieldInfo FieldInfo;

    public readonly Type SourceType; //the type of the instance that the field's value "exists" in (debatable term if the field is private and was declared in a base class)
                                     //that being said SourceType obviously may be different from the field's actual declaring type 
    public FieldObject(ObjectToken token, FieldInfo field, object Object, Type sourceType) : base(token, Object)
    {
        FieldInfo = field;
        SourceType = sourceType;
    }
}

public enum ObjectToken  //tokens are used to figure out what means we will use to read the value
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
}