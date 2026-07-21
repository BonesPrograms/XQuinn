using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using XQuinn.Numerics;



namespace XQuinn.Reflection;





/// <summary>
/// A bridge to get access modifier values from fieldinfos and methodbases, which cannot cast into one another but share the same exact fields.
/// </summary>



/// <summary>
/// Wrapper for a reflection object. Primarily exists to return readable and relatively informative strings about the metadata (it will be roughly as informative as
/// viewing the type directly in code, it lacks deeper metadata information).
/// </summary>

public sealed class ReflectionReader : MetadataReader
{
    public MemberInfo Info => (MemberInfo)Object!;
    public Module Module => Info.Module;
    public Guid ModuleID => Module.ModuleVersionId; //not very wise to index only by metadata token, search for ModuleID/Module + MetadataToken or use the == overloads
    public int MetadataToken => Info.MetadataToken; //only types have guids, other members dont
    public string Name => Info.Name; //may change this to be info.tostring() for type objects (so you can see the namespace), not sure how that will effect methodinfo objects tho
    public readonly MemberGroup? Group; //if is a Type, wont be a Member
    /// <summary>
    /// 32bit byte sequence of the MetadataToken.
    /// </summary>
    public readonly ImmutableArray<byte> TokenAsBytes;
    public readonly Type? Declared;
    public readonly Type? Base;
    public ReflectionReader(MemberInfo info) : base(info)
    {
        TokenAsBytes = [.. BytesLittleEndian.AsBytes(info.MetadataToken)];
        Declared = info.DeclaringType;
        if (info is Type type && type.BaseType != typeof(object))
            Base = type.BaseType;
        Group = MemberGroups.GetGroup(info);
    }

    readonly struct AccessModifiers
    {
        public readonly bool IsPublic;

        public readonly bool IsPrivate;

        public readonly bool IsAssembly;

        public readonly bool IsFamily;

        public readonly bool IsFamilyAndAssembly;

        public readonly bool IsFamilyOrAssembly;

        public AccessModifiers(FieldInfo field)
        {
            IsPublic = field.IsPublic;
            IsPrivate = field.IsPrivate;
            IsAssembly = field.IsAssembly;
            IsFamily = field.IsFamily;
            IsFamilyAndAssembly = field.IsFamilyAndAssembly;
            IsFamilyOrAssembly = field.IsFamilyOrAssembly;
        }

        public AccessModifiers(MethodBase mthd)
        {
            IsPublic = mthd.IsPublic;
            IsPrivate = mthd.IsPrivate;
            IsAssembly = mthd.IsAssembly;
            IsFamily = mthd.IsFamily;
            IsFamilyAndAssembly = mthd.IsFamilyAndAssembly;
            IsFamilyOrAssembly = mthd.IsFamilyOrAssembly;
        }
        public override string ToString()
        {
            if (IsPublic)
                return "public";
            else if (IsFamily)
                return "protected";
            else if (IsPrivate)
                return "private";
            else if (IsAssembly)
                return "internal";
            else if (IsFamilyAndAssembly)
                return "private protected";
            else if (IsFamilyOrAssembly)
                return "protected internal"; //this should literally never throw
            throw new InvalidOperationException("FieldInfo or MethodBase object has invalid access modifiers.");
        }
    }

    /// these methods are helpers for navigating a MetadataMap -
    /// members are returned following their type so if you want to get a class's data you can use IsOrIsDeclaredIn
    public bool EqualsOrIsDeclaredIn(Type type)
    {
        return this == type || DeclaredIn(type);
    }

    // public bool EqualsOrIsDeclaredIn(ReflectionData data)
    // {
    //     return this == data || DeclaredIn(data);
    // }
    // public bool DeclaredIn(ReflectionData data)
    // {
    //     return data.Declared != null && DeclaredIn(data.Declared);
    // }
    public bool DeclaredIn(Type type)
    {
        return Declared?.HasSameMetadataDefinitionAs(type) ?? false;
    }

    public bool Is<T>() where T : MemberInfo => Info is T;

    /// <summary>
    /// Pattern matches the memberinfo object and returns the object, or returns null/throws if the match fails.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? As<T>() where T : MemberInfo => Info as T;

    public T AsOrThrow<T>() where T : MemberInfo
    {
        T? obj = Info as T;
        return obj ?? throw new ArgumentException($"Info is not {typeof(T).Name}");
    }

    /// <summary>
    /// Pattern matches the memberinfo object and returns false if the match fails.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public bool As<T>(out T? obj) where T : MemberInfo
    {
        obj = As<T>();
        return obj != null;
    }

    /// <summary>
    /// This allows you to index Metadata keys in dictionaries without having a reference to the specific instance in the dictionary. As long as they represent the same
    /// MemberInfo object, they will be considered the same.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return Info.GetHashCode();
    }


    //The Equals override for the Metadata class compares the MemberInfo object that it wraps. It compares them by metadata definition rather
    //than object reference. Additionally, the Metadata class can be compared to a MemberInfo object, and will be evaluated in the same manner.

    ///Metadata == memberinfo // vice versa

    public static bool operator !=(MemberInfo? info, ReflectionReader? data) => !(info == data);

    public static bool operator ==(MemberInfo? info, ReflectionReader? data) => data == info;

    public static bool operator !=(ReflectionReader? data, MemberInfo? info) => !(data == info);

    public static bool operator ==(ReflectionReader? data, MemberInfo? info)
    {
        return data?.Equals(info) ?? info == null;
    }

    //Metadata == Metadta
    public static bool operator !=(ReflectionReader? data1, ReflectionReader? data2) => !(data1 == data2);

    public static bool operator ==(ReflectionReader? data1, ReflectionReader? data2)
    {
        return data1?.Equals(data2) ?? data2 is null;
    }

    public override bool Equals(object? obj)
    {
        if (obj is MemberInfo info)
            return info.HasSameMetadataDefinitionAs(Info);
        else if (obj is ReflectionReader data)
            return data.Info.HasSameMetadataDefinitionAs(Info);
        return false;
    }
    //i may add access modifiers - it would be easy, just put my access modifiers right before we call base.tostring()
    //may also want to add other clarifiers like if they are abstract, sealed, virtual, new, etc
    ///will require lots of separate methods and casting, cannot do this like i did MemberToString
    /// other stuff like checking if a metadata is static or instance, such as if a class or property or field is static
    /// maybe the word "method" before methods, "constructor" before constructors
    /// maybe also get generic constraits too, like where T : Memberinfo
    /// 
    protected override StringBuilder ToStringBuilder()
    {
        StringBuilder sb = new();
        sb.Append(MetadataTypeToString());
        sb.Append(base.ToStringBuilder());
        if (Base != null)
        {
            sb.Append(" : ");
            sb.Append(CheckTypeGenerics(Base));
        }
        sb.Append($" Token:: {MetadataToken}");
        sb.Append(" AsBytes:: ");
        foreach (var bits in TokenAsBytes)
        {
            sb.Append($"{bits} ");
        }
        return sb;
    }



    StringBuilder? MetadataTypeToString() => Info switch
    {
        System.Type => TypeToString((Type)Info),
        MethodInfo or ConstructorInfo => MethodToString((MethodBase)Info),
        FieldInfo => FieldToString((FieldInfo)Info),
        _ => null, //lol i never actually used events so im gonna learn them before i start reflecting them
    };

    //ive found this currently isnt necesary because the actual get and setter methods are already being read with their access modifiers shown
    //though it could use a bit more organization, prob will have it find the getters and setters by name get_ set_ and then shift them up to be below their
    //respective property in the list, based on the actual name of the property

    //also this shit wasnt even really working anyways lmao

    // static StringBuilder PropertyToString(PropertyInfo prop)
    // {
    //     StringBuilder sb = new();
    //     MethodInfo? get = prop.GetGetMethod();
    //     if (get != null)
    //     {
    //         sb.Append(MethodToString(get));
    //         sb.Append("get ");
    //     }
    //     MethodInfo? set = prop.GetSetMethod();
    //     if (set != null)
    //     {
    //         sb.Append(MethodToString(set));
    //         sb.Append("set ");
    //     }
    //     return sb;
    // }

    static StringBuilder FieldToString(FieldInfo field)
    {
        StringBuilder sb = new();
        sb.Append(new AccessModifiers(field).ToString() + ' ');
        if (field.IsLiteral)
            sb.Append("const ");
        else if (field.IsStatic)
            sb.Append("static ");
        return sb;

    }

    StringBuilder MethodToString(MethodBase mthd)
    {
        StringBuilder sb = new();
        sb.Append(new AccessModifiers(mthd).ToString() + ' ');
        if (mthd.IsStatic)
        {
            if (mthd is ConstructorInfo)
                sb.Append("static ");
            return sb;
        }
        bool isoverride = false;
        if (Declared != null && mthd is MethodInfo realmethod)
        {
            if (Declared != realmethod.GetBaseDefinition().DeclaringType)
            {
                sb.Append("override ");
                isoverride = true;
            }
        }
        if (mthd.IsFinal)
            sb.Append("sealed ");
        else if (mthd.IsAbstract)
            sb.Append("abstract ");
        else if (!isoverride && mthd.IsVirtual)
            sb.Append("virtual ");
        return sb;
    }



    //also maybe should add stuff that says if it is public, internal, nested private
    static StringBuilder TypeToString(Type type) //need to add stuff for nested types i think
    {
        StringBuilder sb = new();
        if (type.IsAbstract && type.IsSealed)
            sb.Append("static ");
        else if (type.IsAbstract)
            sb.Append("abstract ");
        else if (type.IsSealed)
            sb.Append("sealed ");
        return sb;
    }


}