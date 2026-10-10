using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XQuinn.Reflection;
using XQuinn.Runtime;

namespace XQuinn.ObjectModel
{
    public static class InstanceReader
    {
        public static string Read(object obj)
        {
            Type t = obj.GetType();
            FieldInfo[] fields = t.GetFields(NavigatorCore.Flag);
            PropertyInfo[] props = t.GetProperties(NavigatorCore.Flag);
            Get[] gets = new Get[fields.Length + props.Length];
            for (int i = 0; i < fields.Length; i++)
                gets[i] = new(fields[i]);
            int x = fields.Length;
            for (int i = 0; i < props.Length; i++)
            {
                gets[x] = new(props[i]);
                x++;
            }
            StringBuilder sb = new();
            sb.AppendLine();
            sb.Append("Reading instance of type: ");
            sb.AppendLine(ReflectionPrinter.Print(t, false));
            if (fields.Length > 0)
                sb.AppendLine("Reading fields:");
            for (int i = 0; i < gets.Length; i++)
            {
                Get get = gets[i];
                if (i == fields.Length && props.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Reading properties with getters:");
                }
                if (get.GetValue(obj, out object? got))
                {
                    sb.AppendLine(ReflectionPrinter.Print(get.Getter, false));
                    sb.AppendLine(got?.ToString() ?? "null");
                }
            }
            return sb.ToString();
        }

        readonly struct Get
        {
            public readonly MemberInfo Getter;
            public readonly bool GetValue(object obj, out object? got)
            {
                if (Getter is FieldInfo field)
                {
                    got = field.GetValue(obj);
                    return true;
                }
                else
                {
                    PropertyInfo prop = (PropertyInfo)Getter;
                    MethodInfo? getter = prop.GetGetMethod(true);
                    got = getter?.Invoke(obj, null);
                    return getter != null;
                }
            }
            public Get(MemberInfo getter)
            {
                if (getter is FieldInfo or PropertyInfo)
                    Getter = getter;
                else
                    throw new NotSupportedException();
            }
        }
    }
}