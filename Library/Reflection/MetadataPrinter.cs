using System.Reflection;
using System.Text;
using HarmonyLib;
using System;

namespace XQuinn.Reflection
{



    /// <summary>
    /// Base wrapper class with robust ToString helpers for metadata objects.
    /// </summary>

    internal static class MetadataPrinter
    {

        public static StringBuilder BuildPrint(StringBuilder sb, MemberInfo Object)
        {
            if (Object is MethodInfo mthd)
                MethodToString(sb, mthd);
            else if (Object is ConstructorInfo ctor)
                ConstructorToString(sb, ctor);
            else if (Object is Type t)
                TypeToString(sb, t);
            else
                MemberToString(sb, Object);
            return sb;
        }

        static StringBuilder MemberToString(StringBuilder sb, MemberInfo member)
        {
            sb.Append(member.MemberType.ToString());
            sb.Append(' ');
            GenericTypeToString(sb, member.DeclaringType);
            sb.Append("::");
            GenericTypeToString(sb, member.GetUnderlyingType());
            sb.Append(' ');
            FixGenericString(sb, member.Name);
            return sb;
        }

        public static StringBuilder TypeToString(StringBuilder sb, Type type)
        {
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
        public static StringBuilder ConstructorToString(StringBuilder sb, ConstructorInfo ctor)
        {
            if (ctor.DeclaringType != null) GenericTypeToString(sb, ctor.DeclaringType);
            sb.Append($"::.ctor{ParamsToString(ctor.GetParameters())}");
            return sb;
        }

        public static StringBuilder MethodToString(StringBuilder sb, MethodInfo mthd, bool parameterNames = true)
        {
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
            if (mthd.ReturnType.Name == "Boolean")
            {
                sb.Append("bool");
                return;
            }
            string lowered = mthd.ReturnType.Name.ToLower();
            if (lowered != "string" && lowered != "boolean" && lowered != "void") GenericTypeToString(sb, mthd.ReturnType);
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
                if(arg.IsDefined(typeof(ParamArrayAttribute)))
                txt.Append("params ");
                else if (arg.IsIn) txt.Append("in ");
                else if (arg.IsOut) txt.Append("out ");
                else if (arg.ParameterType.IsByRef) txt.Append("ref ");
                tname.Length = 0;
                GenericTypeToString(tname, arg.ParameterType);
                int index = tname.Length - 1;
                if (tname[index] == '&') tname.Remove(index, 1);
                txt.Append(tname);
                if (names)
                 txt.Append($" {arg.Name}");
                if (For.NeedsDelimiter(args.Length, i))
                 txt.Append(", ");
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
            FixGenericString(sb, type.Name);
            AddGenericArguments(sb, type.GetGenericArguments());
        }


        internal static void FixGenericString(StringBuilder sb, string strng)
        {
            foreach (char c in strng)
                if (c == '`')
                    break;
                else
                    sb.Append(c);
        }

        internal static void AddGenericArguments(StringBuilder sb, Type[]? genericargs)
        {
            if (genericargs?.Length > 0)
            {
                sb.Append('<');
                for (int i = 0; i < genericargs.Length; i++)
                {
                    FixGenericString(sb, genericargs[i].Name);
                    if (For.NeedsDelimiter(genericargs.Length, i)) 
                    sb.Append(", ");
                }
                sb.Append('>');
            }
        }

    }
}