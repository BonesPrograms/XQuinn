using System.Reflection;
using System.Text;
using HarmonyLib;

namespace XQuinn.Reflection;

/// <summary>
/// Base wrapper class with robust ToString helpers for metadata objects.
/// </summary>

public abstract class MetadataReader
{

    //I may add an option later for displaying namespaces on type names. For now we do not to improve readability.
    protected readonly object? Object;
    protected MetadataReader(object? obj)
    {
        Object = obj;
    }

    /// <summary>
    /// Virtual tostring stringbuilder for inheritors.
    /// </summary>
    /// <returns></returns>
    protected virtual StringBuilder ToStringBuilder() => Object switch
    {
        MethodInfo => MethodToString((MethodInfo)Object),
        ConstructorInfo => ConstructorToString((ConstructorInfo)Object),
        FieldInfo or PropertyInfo or EventInfo => MemberToString((MemberInfo)Object),
        Type => TypeToString((Type)Object),
        _ => new StringBuilder(Object?.ToString() ?? "")
    };


    public override sealed string ToString() //inheritors should not invoke tostring in their tostringbuilder override otherwise it will obviously create duplicate stringbuilders
    {
        return ToStringBuilder().ToString();
    }
    static StringBuilder MemberToString(MemberInfo member)
    {
        StringBuilder sb = new();
        sb.Append(member.MemberType.ToString());
        sb.Append(' ');
        sb.Append(CheckTypeGenerics(member.DeclaringType));
        sb.Append("::");
        sb.Append(CheckTypeGenerics(member.GetUnderlyingType()));
        sb.Append(' ');
        sb.Append(FixGenericString(member.Name));
        return sb;
    }

    static StringBuilder TypeToString(Type type)
    {
        StringBuilder sb = new();
        if (typeof(Delegate).IsAssignableFrom(type))
            sb.Append("delegate");
        else if (type.IsEnum)
            sb.Append("enum");
        else if (type.IsArray)
            sb.Append("array");
        else if (type.IsInterface)
            sb.Append("interface");
        else if (type != typeof(string) && (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) || typeof(System.Collections.ICollection).IsAssignableFrom(type)))
            sb.Append("collection");
        else if (type.IsClass)
            sb.Append("class");
        else
            sb.Append("struct");
        sb.Append(' ');
        sb.Append(CheckTypeGenerics(type));
        return sb;
    }
    static StringBuilder ConstructorToString(ConstructorInfo ctor)
    {
        StringBuilder sb = new();
        if (ctor.DeclaringType != null)
        {
            sb.Append(CheckTypeGenerics(ctor.DeclaringType));
        }
        sb.Append($"::.ctor{ParamsToString(ctor.GetParameters())}");
        return sb;
    }

    public static StringBuilder MethodToString(MethodInfo mthd, bool names = false)
    {
        StringBuilder sb = new();
        sb.Append(mthd.IsStatic ? "static " : "instance ");
        GetReturnString(sb, mthd);
        sb.Append(' ');

        sb.Append(CheckTypeGenerics(mthd.DeclaringType));
        sb.Append("::");
        sb.Append(mthd.Name);

        AddGenericArguments(sb, mthd.GetGenericArguments());
        sb.Append(ParamsToString(mthd.GetParameters(), names));

        return sb;
    }

    static void GetReturnString(StringBuilder sb, MethodInfo mthd)
    {
        string lowered = mthd.ReturnType.Name.ToLower();
        if (lowered != "string" && lowered != "bool" && lowered != "void")
            sb.Append($"{CheckTypeGenerics(mthd.ReturnType)}");
        else
            sb.Append(lowered);

    }
    static StringBuilder ParamsToString(ParameterInfo[] args, bool names = false)
    {
        StringBuilder txt = new();
        txt.Append('(');
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            txt.Append(CheckTypeGenerics(arg.ParameterType));
            if (names)
                txt.Append($" {arg.Name}");
            if (args.Length > 1 && i < args.Length - 1)
                txt.Append($", ");
        }
        txt.Append(')');
        return txt;
    }
    /// <summary>
    /// Be careful using this and FixGenericString together or you will not understand why you are producing duplicate name strings.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    protected static StringBuilder? CheckTypeGenerics(Type? type)
    {
        if (type != null)
        {
            Type[] generics = type.GetGenericArguments();
            StringBuilder sb = new();

            sb.Append(FixGenericString(type.Name)); //adds name string here
            AddGenericArguments(sb, generics);
            // if( type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            // sb.Append('?');
            return sb;
        }
        return null;
    }

    static string FixGenericString(string strng)
    {
        if (strng.Length >= 2 && strng[^2] == '`') //i had such a dumb bug with this
            strng = strng[..^2];  //i was wondering why indexing ^2 was throwing, i thought i was returning the index itself and not the actual char struct
        return strng;           //even though i do it right there infront of my own eyes string[^2] == '`'
    }                           // supposed to index ..^2 like this...

    static void AddGenericArguments(StringBuilder sb, Type[]? genericargs)
    {
        if (genericargs?.Length > 0)
        {
            sb.Append('<');
            for (int i = 0; i < genericargs.Length; i++)
            {
                sb.Append(FixGenericString(genericargs[i].Name));
                if (genericargs.Length > 1 && i < genericargs.Length - 1)
                    sb.Append(", ");
            }
            sb.Append('>');
        }
    }

}