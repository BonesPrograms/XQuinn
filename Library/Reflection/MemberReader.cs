using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System;



namespace XQuinn.Reflection
{


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


    /// <summary>
    /// A bridge to get access modifier values from fieldinfos and methodbases, which cannot cast into one another but share the same exact fields.
    /// </summary>



    /// <summary>
    /// Wrapper for a reflection object. Primarily exists to return readable and relatively informative strings about the metadata (it will be roughly as informative as
    /// viewing the type directly in code, it lacks deeper metadata information).
    /// </summary>

    public sealed class MemberReader : MetadataReader
    {

        public static bool ShowToken = false;

        public static bool ShowTokenBytes = false;
        public MemberInfo Info => (MemberInfo)Object!;
        /// <summary>
        /// 32bit byte sequence of the MetadataToken.
        /// </summary>
#if NET6_0_OR_GREATER
        public IReadOnlyList<byte> TokenAsBytes => _tokenAsBytes;

        IReadOnlyList<byte> _tokenAsBytes = null!;
#endif
        public readonly Type? Declared;
        public readonly Type? Base;
        MemberReader(MemberInfo info) : base(info)
        {
            Declared = info.DeclaringType;
            if (info is Type type && type.BaseType != typeof(object))
                Base = type.BaseType;
        }

        public static MemberReader New(MemberInfo info)
        {
            return new(info)
            {
#if NET6_0_OR_GREATER
                _tokenAsBytes = Array.AsReadOnly(Numerics.BytesLittleEndian.AsBytes(info.MetadataToken)),
#endif
            };
        }

        /// <summary>
        /// Quick method for getting a reflectionreader string.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static string String(MemberInfo x) => New(x).ToString();

        protected override StringBuilder ToStringBuilder()
        {
            StringBuilder sb = new();
            sb.Append(MetadataTypeToString());
            sb.Append(base.ToStringBuilder());
            if (Base != null)
            {
                sb.Append(" : ");
                sb.Append(GenericTypeToString(Base));
            }
            if (ShowToken)
            {
                sb.Append($" Token:: {Info.MetadataToken}");
#if NET6_0_OR_GREATER
                sb.Append(" AsBytes:: ");
                if (ShowTokenBytes)
                    foreach (var bits in TokenAsBytes)
                    {
                        sb.Append($"{bits} ");
                    }
#endif
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
}