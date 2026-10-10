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

        public static StringBuilder BuildPrint(StringBuilder sb, MemberInfo Object, bool fullname) =>
        Object switch
        {
            MethodInfo mthd => MethodToString(sb, mthd, fullname),
            ConstructorInfo ctor => ConstructorToString(sb, ctor, fullname),
            Type t => TypeToString(sb, t, fullname),
            PropertyInfo => sb,
            _ => MemberToString(sb, Object, fullname),
        };



        static StringBuilder MemberToString(StringBuilder sb, MemberInfo member, bool fullname)
        {
            sb.Append(ReflectionPrinter.Print(member.GetUnderlyingType(), false));
            sb.Append(' ');
            GenericTypeToString(sb, member.DeclaringType, fullname);
            sb.Append("::");
            GenericTypeToString(sb, member.GetUnderlyingType(), fullname); //maybe this doesnt need to be here cause i already
            sb.Append(' ');         //get it up there
            FixGenericString(sb, member.Name);
            return sb;
        }

        public static StringBuilder TypeToString(StringBuilder sb, Type type, bool fullname)
        {
            if (typeof(Delegate).IsAssignableFrom(type)) sb.Append("delegate");
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
            GenericTypeToString(sb, type, fullname);
            return sb;
        }
        public static StringBuilder ConstructorToString(StringBuilder sb, ConstructorInfo ctor, bool fullname)
        {
            if (ctor.DeclaringType != null)
                GenericTypeToString(sb, ctor.DeclaringType, fullname);
            sb.Append($"::.ctor");
            ParamsToString(sb, ctor.GetParameters(), fullname);
            return sb;
        }

        public static StringBuilder MethodToString(StringBuilder sb, MethodInfo mthd, bool fullname)
        {
            sb.Append(mthd.IsStatic ? "static " : "instance ");
            GetReturnString(sb, mthd, fullname);
            sb.Append(' ');
            GenericTypeToString(sb, mthd.DeclaringType, fullname);
            sb.Append("::");
            sb.Append(mthd.Name);
            AddGenericArguments(sb, mthd.GetGenericArguments(), fullname);
            ParamsToString(sb, mthd.GetParameters(), fullname);
            return sb;
        }

        static void GetReturnString(StringBuilder sb, MethodInfo mthd, bool fullname)
        {
            if (mthd.ReturnType.Name == "Boolean")
            {
                sb.Append("bool");
                return;
            }
            string lowered = mthd.ReturnType.Name.ToLower();
            if (lowered != "string" && lowered != "void")
                GenericTypeToString(sb, mthd.ReturnType, fullname);
            else
                sb.Append(lowered);

        }
        static StringBuilder ParamsToString(StringBuilder txt, ParameterInfo[] args, bool fullname)
        {
            txt.Append('(');
            for (int i = 0; i < args.Length; i++)
            {
                ParameterInfo arg = args[i];
                if (arg.IsDefined(typeof(ParamArrayAttribute)))
                    txt.Append("params ");
                else if (arg.IsIn)
                    txt.Append("in ");
                else if (arg.IsOut)
                    txt.Append("out ");
                else if (arg.ParameterType.IsByRef)
                    txt.Append("ref ");
                GenericTypeToString(txt, arg.ParameterType, fullname);
                txt.Append($" {arg.Name}");
                if (arg.HasDefaultValue)
                {
                    object? value = arg.DefaultValue;
                    string? ret = value != null ? value.ToString() : arg.ParameterType.IsClass ? "null" : "default";
                    txt.Append($" = {ret}");
                }
                if (For.SmartDelimiter(args.Length, i))
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
        public static void GenericTypeToString(StringBuilder sb, Type? type, bool fullname)
        {
            if (type == null) return;
            FixGenericString(sb, fullname ? type.FullName ?? type.Name : type.Name);
            AddGenericArguments(sb, type.GetGenericArguments(), fullname);
        }


        internal static void FixGenericString(StringBuilder sb, string strng)
        {
            foreach (char c in strng)
                if (c == '`')
                    break;
                else
                    sb.Append(c);
        }

        internal static void AddGenericArguments(StringBuilder sb, Type[]? genericargs, bool fullname)
        {
            if (genericargs?.Length > 0)
            {
                sb.Append('<');
                for (int i = 0; i < genericargs.Length; i++)
                {
                    FixGenericString(sb, fullname ? genericargs[i].FullName ?? genericargs[i].Name : genericargs[i].Name);
                    if (genericargs[i].IsGenericType)
                        AddGenericArguments(sb, genericargs[i].GetGenericArguments(), fullname);
                    if (For.SmartDelimiter(genericargs.Length, i))
                        sb.Append(", ");
                }
                sb.Append('>');
            }
        }

    }
}