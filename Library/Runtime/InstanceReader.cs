using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using XQuinn.IO;
using XQuinn.Reflection;
using XQuinn.Reflection.Tokenizer;


namespace XQuinn.Runtime;




//This does not read through the fields of GameObject types because fields like Zone and Cell will DESTROY the console
//Instead GameObject Scanner is used when a GameObject is detected as a field in a component
//GameObject elements in Collections are read "shallow" to avoid clogging console

//If you want to read a part from a GameObject that only exists as a field in your component, you will need to instance this class in code
//and pass the desired part from your GameObject field
//such as mutation equipment

//In the future I may add a way for you to name the field and one of its parts so that it can be read, but for now this is what you get

//You can use "ReadClass" arbitrarily, you can send any type through the scanner, even if it is not an IComponent<GameObject>
//but you will have to do that in code

//Parts like Body, Brain and Physics cannot be completely read, because it would become very unreadable
//Declared fields will be read, but as member access chains increase, object information will be skipped
//If you want to read specific details like that, you need to instance this type in code

//LoopLimit can be null - will stop reading at System.Object
//if reading a component in code, should mark this as typeof(IPart) or typeof(Effect) otherwise you will read the parentobject and it will clog console

//Note - "cameFromReferenceType" should actually say "cameFromClassOrStruct", it is for reading classes or nonprimitive structs, i am just too lazy
//to rename it (this used to not support reading nonprimitive structs because i never used a struct before wehn i wrote this and forgot i need to be able
//to read them lol)

//Note it should not say Custom Type, we should add some properties to the Object class that clarifies if this is a class or struct
//And then we can use that info to print the string "class" or "struct" instead of Custom Type

public class InstanceReader : MetadataReader, IDisposable
{

    public void Dispose() => Writer.Close();

    void Write(string txt) => Writer.WriteLine(txt); //rewrote the code to use a streamwriter and im lazy, method was already called "Write"

    public Type? LoopLimit;

    readonly StreamWriter Writer;

    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static;
    //loops through all base types so its declared only, but "source type" is tracked (the inheritor at the very end) if its a Field

    public InstanceReader(string path, Type? loopLimit = null) : base(null)
    {
        LoopLimit = loopLimit;
        XQuinn.IO.Write.SafetyCheck(path);
        Writer = new(path);
    }
    public void Read<T>(T component, int skip = 0) where T : notnull
    {
        Skip(skip);
        string msg = $"Beginning read of fields in {component.GetType()}";
        Write(msg);
        Skip(2);
        ReadClass(component);
        Dispose();
    }

    //For overriding in inheritors incase you want to give a specific type a custom read (for example, a qud gameobject, since a normal read would be uninformative)
    protected virtual void ReadClass(object classObj, bool cameFromCollection = false, bool cameFromReferenceType = false)
    {
       // if (!ReadClassCustom(classObj, cameFromCollection, cameFromReferenceType))
    //        return;
        Type objectType = classObj.GetType();
        Write($"Beginning read for fields of type {objectType}.");
        Skip(1);
        LoopInheritance(classObj, objectType, cameFromCollection, cameFromReferenceType);
    }

    // /// <summary>
    // /// Return false to skip original readclass.
    // /// </summary>
    // /// <param name="obj"></param>
    // /// <returns></returns>
    // protected virtual bool ReadClassCustom(object obj, bool cameFromCollection, bool cameFromReferenceType)
    // {
    //     return true;
    // }

    protected void LoopInheritance(object classObj, Type sourceType, bool cameFromCollection, bool cameFromReferenceType)
    {
        Type? varyingType = sourceType;
        while (varyingType != LoopLimit && varyingType != null && varyingType != typeof(object))
        {
            FieldInfo[] fields = varyingType!.GetFields(Flags);
            List<Field> sortedFields = SortFields(classObj, fields, sourceType);
            foreach (Field field in sortedFields)
                ReadObject(field, field.FieldInfo, field.SourceType, cameFromCollection, cameFromReferenceType);
            varyingType = varyingType.BaseType;
        }
    }


    //cameFromCollection is a little ambiguous here : it does not mean this object is an element in a collection
    //that is what isInCollection means (because we are receiving an <Element> object rather than a <Field> object)
    //cameFromCollection means we are reading a field in a custom type that is an element in a collection
    protected void ReadObject(BaseObject info, FieldInfo? field, Type? sourceType, bool cameFromCollection, bool cameFromReferenceType)
    {
        Skip(1);
        object? obj = info.Instance;
        Token token = info.Token;
        bool isInCollection = info is Element; //field and sourcetype will be null
        ReadObjectBasic(info, field, sourceType, isInCollection);
        switch (token)
        {
            case Token.IDictionary:
                {
                    if (cameFromReferenceType)
                        goto case Token.ClassOrStruct;
                    if (!isInCollection && !cameFromCollection)
                        ReadIDictionary((IDictionary)obj!);
                    else
                        Write("Detected a collection as an element in a collection or a field for a custom type in a collection, skipping for readability.");
                }
                break;
            case Token.IList:
                {
                    if (cameFromReferenceType)
                        goto case Token.ClassOrStruct;
                    if (!isInCollection && !cameFromCollection)     //reading collections inside of collections is not easy to understand so it is not allowed
                        ReadIList((IList)obj!);
                    else
                        Write("Detected a collection as an element in a collection or a field for a custom type in a collection, skipping for readability.");
                }
                break;
            case Token.ClassOrStruct:
                {
                    if (cameFromReferenceType) //member access chains that are larger than one are really hard to keep track of in your brain when reading the log, so they are skipped
                        Write("Detected custom type as a field in a custom type. Skipping for readability."); //they also clog the console
                    else if (!cameFromCollection)
                    {
                        Write($"{(isInCollection ? "element" : $"\"{field!.Name}\"")} is a custom type, reading fields.");
                        ReadClass(obj!, isInCollection, true);
                    }
                    else if (!isInCollection)
                        Write($"{$"\"{field!.Name}\""} is a custom type, but is a field for a custom type that is an element in a collection. Skipping for readability.");
                    else
                        Write("Detected custom type element in a multi-collection chain. Skipping for readability.");
                }       //parts like Brain will make the console EXPLODE if we read these, some of their collections have deep member access chains, such as PartyMembers
                break;  //you cant really read those collections properly with this Type at all
        }

    }

    protected void ReadObjectBasic(BaseObject info, FieldInfo? field, Type? sourceType, bool isInCollection)
    {
        StringBuilder text = new(); //sourceType and field will be null if in collection
        text.Append($"Reading {(isInCollection ? "ELEMENT" : $"FIELD \"{field!.Name}\"")} from {(isInCollection ? "collection" : $"type {CheckTypeGenerics(sourceType)}")}:\n");
        text.Append($"Type: {(isInCollection ? info.IsNull ? "null value in keyValuePair, cannot get type info" : CheckTypeGenerics(info.Instance!.GetType()) : CheckTypeGenerics(field!.FieldType))}\n");
        text.Append(DisplayValue(info)); //I have prevented lists from sending null elements, and IDictionary from sending null keys, but keys with null values are permitted
        if (!isInCollection)                //so in those cases it is possible for us to be unable to retrieve the value's type
        {                                   //though you can just see the dictionary's generic arguments to get an idea of what type it would've been
            text.Append($"Declared in: {CheckTypeGenerics(field!.DeclaringType)}\n");
            text.Append($"Attributes {field.Attributes}\n");
        }
        Write(text.ToString());

    }

    void ReadIList(IList list)
    {
        Skip(1);
        Write("READING ILIST OF COUNT " + list.Count.ToString());
        List<Element> sortedElements = SortElements(list);
        foreach (var element in sortedElements)
        {
            ReadObject(element, null, null, false, false);
        }
        Skip(1);
        Write("ILIST FINISHED");
        Skip(1);
    }

    void ReadIDictionary(IDictionary dic)
    {
        Skip(1);
        Write("READING IDICTIONARY OF COUNT " + dic.Count.ToString());
        IDictionaryEnumerator enumerator = dic.GetEnumerator();
        Dictionary<Element, Element> elementDictionary = new();
        while (enumerator.MoveNext())
        {
            if (enumerator.Key != null)
            {
                Token keytoken = BaseObject.GetToken(enumerator.Key);
                Token valuetoken = BaseObject.GetToken(enumerator.Value);
                elementDictionary[new Element(keytoken, enumerator.Key)] = new Element(valuetoken, enumerator.Value);
            }
        }
        int count = 1;
        foreach (var pair in elementDictionary)
        {
            Skip(1);
            Write($"KEY: {count}");
            ReadObject(pair.Key, null, null, false, false);
            Skip(1);
            Write($"VALUE: {count}");
            ReadObject(pair.Value, null, null, false, false);
            count++;
        }
        Skip(1);
        Write("IDICTIONARY FINISHED");
        Skip(1);

    }


    static List<Field> SortFields(object classObj, FieldInfo[] fields, Type sourceType)
    {
        List<Field> fieldDetails = new(fields.Length);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            object? fieldObj = field.GetValue(classObj);
            Token token = BaseObject.GetToken(fieldObj);
            fieldDetails.Add(new Field(token, field, fieldObj!, sourceType));
        }
        return SortByToken(fieldDetails);
    }

    static List<Element> SortElements(IList list) //on the off chance you have a List<object>
    {                                       //you cant control dictionary order and ObjectInfo doesn't support making a "KeyValue" class for a theoretical List<KeyValue> (i tried it, its a mess)
        List<Element> elements = new();     //so we dont sort dictionaries by order
        for (int i = 0; i < list.Count; i++)
        {
            object? element = list[i];
            if (element != null)
            {
                Token token = BaseObject.GetToken(element);
                elements.Add(new Element(token, element));
            }
        }
        return SortByToken(elements);
    }

    static List<O> SortByToken<O>(List<O> infoObjects) where O : BaseObject
    {
        List<O> referenceTypes = new();
        List<O> simpleTypes = new();
        List<O> collections = new();
        foreach (var info in infoObjects)
        {
            if (info.IsCollection)
                collections.Add(info);
            else if (info.IsSimple || info.IsNull)
                simpleTypes.Add(info);
            else if (info.IsReferenceType)
                referenceTypes.Add(info);
        }
        List<O> sortedInfo = new(infoObjects.Count);
        sortedInfo.AddRange(simpleTypes);
        sortedInfo.AddRange(collections);
        sortedInfo.AddRange(referenceTypes);
        return sortedInfo; //rearranges order
    }


    static string DisplayValue(BaseObject info)
    {
        string msg = $"Value: {$"{info.Instance}" ?? "null"}\n";
        if (info.IsSimple && !info.IsNull)
        {

            string valueDisplay = SimpleValueDisplay(info.Instance, info.Token);
            msg = $"Value: {valueDisplay}\n";
        }
        return msg;

    }

    // static string CheckRef(object? obj, Token token) => token switch
    // {
    //     Token.IDictionary or Token.IList => ReferenceTypeValueDisplay(obj),
    //     _=> $"{obj}" //custom type tostring overload is taken, though usually this value is meaningless
    // };

    static string SimpleValueDisplay(object? obj, Token token) =>
    token switch
    {
        Token.Boolean => ShowBoolValue((bool)obj!),
        Token.String or Token.Enum => $"\"{obj}\"", //gives strings an enums an orange stringy color
        _ => $"{obj}", //integers always look blue and custom structs have the option of string overloads
    };

    static string ReferenceTypeValueDisplay(object? obj)
    {

        return CheckTypeGenerics(obj?.GetType())?.ToString() ?? "null";

    }

    static string ShowBoolValue(bool boolean)
    {
        return boolean ? "true" : "false";
    }

    void Skip(int value)
    {
        value--;
        for (int i = 0; i < value; i++)
            Write("\n");
    }



}


