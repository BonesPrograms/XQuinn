using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using XQuinn.IO;
using XQuinn.Reflection;
using System.IO;


namespace XQuinn.ObjectModel
{




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

    public class InstanceReader : MetadataPrinter, IDisposable
    {

        public abstract class TokenizedObject
        {
            public readonly ObjectToken Token;

            public readonly object? Instance;

            internal TokenizedObject(ObjectToken token, object? Object)
            {
                this.Token = token;
                this.Instance = Object;
            }

            public bool IsNull => Token == ObjectToken.Null;

            public bool IsReferenceType => Token == ObjectToken.ClassOrStruct;

            public bool IsCollection => Token == ObjectToken.IList || Token == ObjectToken.IDictionary || Token == ObjectToken.ICollection;

            public bool IsSimple => Token switch
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
                    ValueType => EvaluateValueType((ValueType)obj),
                    IDictionary => ObjectToken.IDictionary,
                    IList => ObjectToken.IList,
                    Delegate => ObjectToken.Delegate, //this may require extra work, dont know how to read the delegate value yet
                    ICollection => ObjectToken.ICollection,
                    _ => ObjectToken.ClassOrStruct
                };
            static ObjectToken EvaluateValueType(ValueType valuetype) => valuetype.GetType().IsPrimitive ? ObjectToken.PrimitiveStruct : ObjectToken.ClassOrStruct;

        }



        public sealed class ElementObject : TokenizedObject
        {
            internal  ElementObject(ObjectToken token, object? Object) : base(token, Object)
            {
            }
        }

        public sealed class FieldObject : TokenizedObject
        {
            public readonly FieldInfo FieldInfo;

            public readonly Type SourceType; //the type of the instance that the field's value "exists" in (debatable term if the field is private and was declared in a base class)
                                             //that being said SourceType obviously may be different from the field's actual declaring type 
            internal  FieldObject(ObjectToken token, FieldInfo field, object Object, Type sourceType) : base(token, Object)
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
            ICollection,
            PrimitiveStruct,
            String, //strings are classes read like primitive structs
            ClassOrStruct, //Classes and nonprimitive structs (called "Custom Types" by the reader)
            Enum,
            Delegate,
            Boolean //booltostring is converted to its lower varian
        }
        public void Dispose()
        {
            Writer.Close();
            GC.SuppressFinalize(this);
        }

        protected void Write(string txt) => Writer.WriteLine(txt); //rewrote the code to use a streamwriter and im lazy, method was already called "Write"

        public Type LoopLimit;
        protected readonly StreamWriter Writer = null!;
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static;
        //loops through all base types so its declared only, but "source type" is tracked (the inheritor at the very end) if its a Field

        InstanceReader(string path, Type? loopLimit = null) : base(null)
        {
            LoopLimit = loopLimit ?? typeof(object);
            Writer = new(path);

        }
        public static InstanceReader New(string outputFilePath, bool makeFileIfNotFound, Type? loopLimit = null)
        {
            if (makeFileIfNotFound)
                XQuinn.IO.Logger.SafetyCheck(outputFilePath);
            return new(outputFilePath, loopLimit);
        }
        public void Read(object instance, int skip = 0)
        {
            Skip(skip);
            Type instanceType = instance.GetType();
            string msg = $"Beginning read of fields in {instanceType}";
            if (LoopLimit == typeof(object))
                LoopLimit = LoopLimiter(instanceType);
            Write(msg);
            Skip(2);
            ReadClass(LoopLimit, instance);
        }

        //For overriding in inheritors incase you want to give a specific type a custom read (for example, a qud gameobject, since a normal read would be uninformative)
        protected virtual void ReadClass(Type? limit, object classObj, bool cameFromCollection = false, bool cameFromReferenceType = false)
        {
            // if (!ReadClassCustom(classObj, cameFromCollection, cameFromReferenceType))
            //        return;
            Type objectType = classObj.GetType();
            Write($"Beginning read for fields of type {objectType}.");
            Skip(1);

            LoopInheritance(limit ?? LoopLimiter(objectType), classObj, objectType, cameFromCollection, cameFromReferenceType);
        }

        static Type LoopLimiter(Type t)
        {
            if (t.IsEnum)
                return typeof(Enum);
            if (t.IsValueType)
                return typeof(ValueType);
            return typeof(object);
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

        protected void LoopInheritance(Type? loopLimit, object classObj, Type sourceType, bool cameFromCollection, bool cameFromReferenceType)
        {
            Type? varyingType = sourceType;
            while (varyingType != LoopLimit && varyingType != null && varyingType != typeof(object) && varyingType != typeof(ValueType))
            {
                FieldInfo[] fields = varyingType!.GetFields(Flags);
                List<FieldObject> sortedFields = SortFields(classObj, fields, sourceType);
                foreach (FieldObject field in sortedFields)
                    ReadObject(field, field.FieldInfo, field.SourceType, cameFromCollection, cameFromReferenceType);
                varyingType = varyingType.BaseType;
            }
        }


        //cameFromCollection is a little ambiguous here : it does not mean this object is an element in a collection
        //that is what isInCollection means (because we are receiving an <Element> object rather than a <Field> object)
        //cameFromCollection means we are reading a field in a custom type that is an element in a collection
        protected void ReadObject(TokenizedObject info, FieldInfo? field, Type? sourceType, bool cameFromCollection, bool cameFromReferenceType)
        {
            Skip(1);
            object? obj = info.Instance;
            ObjectToken token = info.Token;
            bool isInCollection = info is ElementObject; //field and sourcetype will be null
            ReadObjectBasic(info, field, sourceType, isInCollection);
            switch (token)
            {
                case ObjectToken.IDictionary:
                    {
                        if (cameFromReferenceType)
                            goto case ObjectToken.ClassOrStruct;
                        if (!isInCollection && !cameFromCollection)
                            ReadIDictionary((IDictionary)obj!);
                        else
                            Write("Detected a collection as an element in a collection or a field for a custom type in a collection, skipping for readability.");
                    }
                    break;
                case ObjectToken.ICollection or ObjectToken.IList:
                    {
                        if (cameFromReferenceType)
                            goto case ObjectToken.ClassOrStruct;
                        if (!isInCollection && !cameFromCollection)     //reading collections inside of collections is not easy to understand so it is not allowed
                            ReadIList((ICollection)obj!);
                        else
                            Write("Detected a collection as an element in a collection or a field for a custom type in a collection, skipping for readability.");
                    }
                    break;
                case ObjectToken.ClassOrStruct:
                    {
                        if (cameFromReferenceType) //member access chains that are larger than one are really hard to keep track of in your brain when reading the log, so they are skipped
                            Write("Detected custom type as a field in a custom type. Skipping for readability."); //they also clog the console
                        else if (!cameFromCollection)
                        {
                            Write($"{(isInCollection ? "element" : $"\"{field!.Name}\"")} is a custom type, reading fields.");
                            ReadClass(null, obj!, isInCollection, true);
                        }
                        else if (!isInCollection)
                            Write($"{$"\"{field!.Name}\""} is a custom type, but is a field for a custom type that is an element in a collection. Skipping for readability.");
                        else
                            Write("Detected custom type element in a multi-collection chain. Skipping for readability.");
                    }       //parts like Brain will make the console EXPLODE if we read these, some of their collections have deep member access chains, such as PartyMembers
                    break;  //you cant really read those collections properly with this Type at all
            }

        }
        string? GenericTypeToString(Type? type)
        {
            StringBuilder sb = new();
            if (type == null) return null;
            sb.Append(FixGenericString(type.Name)); //adds name string here
            AddGenericArguments(sb, type.GetGenericArguments());
            return sb.ToString();
        }
        protected void ReadObjectBasic(TokenizedObject info, FieldInfo? field, Type? sourceType, bool isInCollection)
        {
            StringBuilder text = new(); //sourceType and field will be null if in collection
            text.Append($"Reading {(isInCollection ? "ELEMENT" : $"FIELD \"{field!.Name}\"")} from {(isInCollection ? "collection" : $"type {GenericTypeToString(sourceType)}")}:{Environment.NewLine}");
            text.Append($"Type: {(isInCollection ? info.IsNull ? "null value in keyValuePair, cannot get type info" : GenericTypeToString(info.Instance!.GetType()) : GenericTypeToString(field!.FieldType))}{Environment.NewLine}");
            text.Append(DisplayValue(info)); //I have prevented lists from sending null elements, and IDictionary from sending null keys, but keys with null values are permitted
            if (!isInCollection)                //so in those cases it is possible for us to be unable to retrieve the value's type
            {                                   //though you can just see the dictionary's generic arguments to get an idea of what type it would've been
                text.Append($"Declared in: ");
                GenericTypeToString(text, field!.DeclaringType);
                text.Append(Environment.NewLine);
                text.Append($"Attributes {field.Attributes}{Environment.NewLine}");
            }
            Write(text.ToString());

        }

        void ReadIList(ICollection list)
        {
            Skip(1);
            Write("READING ILIST OF COUNT " + list.Count.ToString());
            List<ElementObject> sortedElements = SortElements(list);
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
            Dictionary<ElementObject, ElementObject> elementDictionary = new();
            while (enumerator.MoveNext())
            {
                if (enumerator.Key != null)
                {
                    ObjectToken keytoken = TokenizedObject.GetToken(enumerator.Key);
                    ObjectToken valuetoken = TokenizedObject.GetToken(enumerator.Value);
                    elementDictionary[new ElementObject(keytoken, enumerator.Key)] = new ElementObject(valuetoken, enumerator.Value);
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


        static List<FieldObject> SortFields(object classObj, FieldInfo[] fields, Type sourceType)
        {
            List<FieldObject> fieldDetails = new(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object? fieldObj = field.GetValue(classObj);
                ObjectToken token = TokenizedObject.GetToken(fieldObj);
                fieldDetails.Add(new FieldObject(token, field, fieldObj!, sourceType));
            }
            return SortByToken(fieldDetails);
        }

        static List<ElementObject> SortElements(ICollection list) //on the off chance you have a List<object>
        {                                       //you cant control dictionary order and ObjectInfo doesn't support making a "KeyValue" class for a theoretical List<KeyValue> (i tried it, its a mess)
            List<ElementObject> elements = new();     //so we dont sort dictionaries by order
            foreach (var element in list)
            {
                //  if (element != null)
                //  {
                ObjectToken token = TokenizedObject.GetToken(element);
                elements.Add(new ElementObject(token, element));
                //  }
            }
            return SortByToken(elements);
        }

        static List<T> SortByToken<T>(List<T> infoObjects) where T : TokenizedObject
        {
            List<T> referenceTypes = new();
            List<T> simpleTypes = new();
            List<T> collections = new();
            foreach (var info in infoObjects)
            {
                if (info.IsCollection)
                    collections.Add(info);
                else if (info.IsSimple || info.IsNull)
                    simpleTypes.Add(info);
                else if (info.IsReferenceType)
                    referenceTypes.Add(info);
            }
            List<T> sortedInfo = new(infoObjects.Count);
            sortedInfo.AddRange(simpleTypes);
            sortedInfo.AddRange(collections);
            sortedInfo.AddRange(referenceTypes);
            return sortedInfo; //rearranges order
        }


        static string DisplayValue(TokenizedObject info)
        {
            string msg = $"Value: {$"{info.Instance}" ?? "null"}{Environment.NewLine}";
            if (info.IsSimple && !info.IsNull)
            {

                string valueDisplay = SimpleValueDisplay(info.Instance, info.Token);
                msg = $"Value: {valueDisplay}{Environment.NewLine}";
            }
            return msg;

        }

        // static string CheckRef(object? obj, Token token) => token switch
        // {
        //     Token.IDictionary or Token.IList => ReferenceTypeValueDisplay(obj),
        //     _=> $"{obj}" //custom type tostring overload is taken, though usually this value is meaningless
        // };

        static string SimpleValueDisplay(object? obj, ObjectToken token) =>
        token switch
        {
            ObjectToken.Boolean => ShowBoolValue((bool)obj!),
            ObjectToken.String or ObjectToken.Enum => $"\"{obj}\"", //gives strings an enums an orange stringy color
            _ => $"{obj}", //integers always look blue and custom structs have the option of string overloads
        };

        static string ShowBoolValue(bool boolean)
        {
            return boolean ? "true" : "false";
        }

        protected void Skip(int value)
        {
            for (int i = 1; i <= value; i++)
                Write(Environment.NewLine);
        }



    }


}