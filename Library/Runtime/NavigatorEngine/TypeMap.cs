using System;
using System.Reflection;
using System.Collections.Generic;
using static XQuinn.Runtime.NavigatorCore;
using System.Runtime.CompilerServices;
using System.Linq;

namespace XQuinn.Runtime.NavigatorEngine
{

    internal static class TypeMap
    {

        internal static void MapType(Dictionary<MethodKey, MethodBase>? _methods, Dictionary<string, FieldInfo>? _fields, Type type, Dictionary<string, PropertyInfo>? _props, bool @new = true)
        {

            if (_methods != null)
            {
                _methods.Clear();
                List<MethodBase> methods = GetUnfilteredMethods(type, @new);
                FilterSupportedMethods(methods);
                GenericKey[] distinctKeys = methods.Select(x=> new GenericKey(x)).Distinct().ToArray();
                foreach (GenericKey key in distinctKeys)
                    SortMethods(methods, key, _methods);
            }
            if (_fields != null)
            {
                _fields.Clear();
                AddBackwards(_fields, type!.GetFields(Flag));
            }
            if (_props != null)
            {
                _props.Clear();
                PropertyInfo[] properties = type.GetProperties(Flag);
                AddBackwards(_props, properties, x => x.GetIndexParameters().Length == 0);
            }
        }



        internal static List<MethodBase> GetUnfilteredMethods(Type type, bool @new)
        {
            List<MethodBase> methods = new(type.GetMethods(Flag));
            if (@new == true) //we always get methods even if new == true, because methods can also be named new of course
            {
                ConstructorInfo[] ctors = type.GetConstructors(Flag);
                methods.AddRange(ctors);
            }
            return methods;
        }

        static void FilterSupportedMethods(List<MethodBase> methods)
        {
            for (int i = methods.Count - 1; i >= 0; i--)
            {
                MethodBase method = methods[i];
                if (method.GetCustomAttribute<CompilerGeneratedAttribute>() != null || !Reflector.SupportedMember(method, method.GetParameters()))
                    methods.Remove(method);
            }
        }
        static void AddBackwards<T>(Dictionary<string, T> storage, T[] memberArray, Predicate<T>? pred = null) where T : MemberInfo
        {
            for (int i = memberArray.Length - 1; i >= 0; i--)
            {
                T member = memberArray[i];
                if (member.GetCustomAttribute<CompilerGeneratedAttribute>() == null && (pred?.Invoke(member) ?? true))
                    storage[member.Name] = member;
            }
        }

        static void SortMethods(List<MethodBase> methods, GenericKey key, Dictionary<MethodKey, MethodBase> _methods)
        {
            int matches = 0;
            for (int i = 0; i < methods.Count; i++)
            {
                MethodBase method = methods[i];
                GenericKey gkey = new(method);
                if (gkey == key)
                {
                    MethodKey mkey = new(matches, gkey);
                    _methods[mkey] = method;
                    methods.Remove(method);
                    i--;
                    matches++;
                }

            }

        }



    }
}