using System.Reflection;
using System.Text;
using HarmonyLib;
using System;

namespace XQuinn.Reflection
{



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
            GenericTypeToString(sb, member.DeclaringType);
            sb.Append("::");
            GenericTypeToString(sb, member.GetUnderlyingType());
            sb.Append(' ');
            sb.Append(FixGenericString(member.Name));
            return sb;
        }

        static StringBuilder TypeToString(Type type)
        {
            StringBuilder sb = new();
            if (typeof(Delegate).IsAssignableFrom(type)) sb.Append("delegate");
            else if (type.IsEnum) sb.Append("enum");
            else if (type.IsArray) sb.Append("array");
            else if (type.IsInterface) sb.Append("interface");
            else if (type != typeof(string) && (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) || typeof(System.Collections.ICollection).IsAssignableFrom(type))) sb.Append("collection");
            else if (type.IsClass) sb.Append("class");
            else sb.Append("struct");
            sb.Append(' ');
            GenericTypeToString(sb, type);
            return sb;
        }
        static StringBuilder ConstructorToString(ConstructorInfo ctor)
        {
            StringBuilder sb = new();
            if (ctor.DeclaringType != null) GenericTypeToString(sb, ctor.DeclaringType);
            sb.Append($"::.ctor{ParamsToString(ctor.GetParameters())}");
            return sb;
        }

        public static StringBuilder MethodToString(MethodInfo mthd, bool parameterNames = false)
        {
            StringBuilder sb = new();
            sb.Append(mthd.IsStatic ? "static " : "instance ");
            GetReturnString(sb, mthd);
            sb.Append(' ');
            GenericTypeToString(sb, mthd.DeclaringType);
            sb.Append("::");
            sb.Append(mthd.Name);
            AddGenericArguments(sb, mthd.GetGenericArguments());
            sb.Append(ParamsToString(mthd.GetParameters(), parameterNames));

            return sb;
        }

        static void GetReturnString(StringBuilder sb, MethodInfo mthd)
        {
            string lowered = mthd.ReturnType.Name.ToLower();
            if (lowered != "string" && lowered != "bool" && lowered != "void") GenericTypeToString(sb, mthd.ReturnType);
            else sb.Append(lowered);

        }
        static StringBuilder ParamsToString(ParameterInfo[] args, bool names = false)
        {
            StringBuilder txt = new();
            StringBuilder tname = new();
            txt.Append('(');
            for (int i = 0; i < args.Length; i++)
            {
                ParameterInfo arg = args[i];
                if (arg.IsIn) txt.Append("in ");
                else if (arg.IsOut) txt.Append("out ");
                else if (arg.ParameterType.IsByRef) txt.Append("ref ");
                tname.Length = 0;
                GenericTypeToString(tname, arg.ParameterType);
                int index = tname.Length - 1;
                if (tname[index] == '&') tname.Remove(index, 1);
                txt.Append(tname);
                if (names) txt.Append($" {arg.Name}");
                if (For.Multiples(args.Length, i)) txt.Append(", ");
            }
            txt.Append(')');
            return txt;
        }
        /// <summary>
        /// Be careful using this and FixGenericString together or you will not understand why you are producing duplicate name strings.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static void GenericTypeToString(StringBuilder sb, Type? type)
        {
            if (type == null) return;
            sb.Append(FixGenericString(type.Name)); //adds name string here
            AddGenericArguments(sb, type.GetGenericArguments());
        }
        

        static string FixGenericString(string strng)
        {
            if (strng.Length >= 2 && strng[strng.Length - 2] == '`') strng = strng.Substring(0, strng.Length - 2);
            return strng;
        }

        static void AddGenericArguments(StringBuilder sb, Type[]? genericargs)
        {
            if (genericargs?.Length > 0)
            {
                sb.Append('<');
                for (int i = 0; i < genericargs.Length; i++)
                {
                    sb.Append(FixGenericString(genericargs[i].Name));
                    if (For.Multiples(genericargs.Length, i)) sb.Append(", ");
                }
                sb.Append('>');
            }
        }

    }
}