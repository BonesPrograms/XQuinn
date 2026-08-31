using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System;



namespace XQuinn.Reflection
{

    /// <summary>
    /// A bridge to get access modifier values from fieldinfos and methodbases, which cannot cast into one another but share the same exact fields.
    /// </summary>




    /// <summary>
    /// Wrapper for a reflection object. Primarily exists to return readable and relatively informative strings about the metadata (it will be roughly as informative as
    /// viewing the type directly in code, it lacks deeper metadata information).
    /// </summary>

    public static class ReflectionPrinter
    {
        /// <summary>
        /// Quick method for getting a reflectionreader string.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static string Print(MemberInfo Info)
        {
            StringBuilder sb = new();
            MetadataTypeToString(sb, Info);
            MetadataPrinter.BuildPrint(sb, Info);
            if (Info is Type t && t.BaseType != typeof(object) && t.BaseType != null)
            {
                sb.Append(" : ");
                MetadataPrinter.GenericTypeToString(sb, t.BaseType);
            }
            return sb.ToString();
        }

        static void MetadataTypeToString(StringBuilder sb, MemberInfo info)
        {
            if (info is Type t)
                TypeToString(sb, t);
            else if (info is MethodBase m)
                MethodToString(sb, m);
            else if (info is FieldInfo f)
                FieldToString(sb, f);

        }

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

        static StringBuilder FieldToString(StringBuilder sb, FieldInfo field)
        {
            sb.Append(new AccessModifiers(field).ToString() + ' ');
            if (field.IsLiteral) return sb.Append("const ");
            else if (field.IsStatic) return sb.Append("static ");
            return sb;

        }

        static StringBuilder MethodToString(StringBuilder sb, MethodBase mthd)
        {
            sb.Append(new AccessModifiers(mthd).ToString() + ' ');
            if (mthd is ConstructorInfo)
                return mthd.IsStatic ? sb.Append("static ") : sb;
            return MethodToString(sb, (MethodInfo)mthd);
        }

        static StringBuilder MethodToString(StringBuilder sb, MethodInfo mthd)
        {
            bool overriden = false;
            if (mthd.DeclaringType != null)
            {
                overriden = mthd.DeclaringType != mthd.GetBaseDefinition().DeclaringType;
                if (overriden)
                    sb.Append("override ");
            }
            if (mthd.IsFinal)
                return sb.Append("sealed ");
            else if (mthd.IsAbstract)
                return sb.Append("abstract ");
            else if (!overriden && mthd.IsVirtual)
                return sb.Append("virtual ");
            return sb;
        }



        //also maybe should add stuff that says if it is public, internal, nested private
        static StringBuilder TypeToString(StringBuilder sb, Type type) //need to add stuff for nested types i think
        {
            if (type.IsAbstract && type.IsSealed) return sb.Append("static ");
            else if (type.IsAbstract) return sb.Append("abstract ");
            else if (type.IsSealed) return sb.Append("sealed ");
            return sb;
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


    }




}