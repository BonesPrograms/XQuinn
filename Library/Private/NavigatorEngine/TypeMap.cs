using System;
using System.Reflection;
using System.Collections.Generic;
using static XQuinn.Runtime.Navigator;
using System.Runtime.CompilerServices;
using XQuinn.Reflection;
using System.Linq;
using XQuinn.CodeAnalysis.AST;
using System.Text;

namespace XQuinn.Private.NavigatorEngine
{

    internal readonly struct MethodKey : IEquatable<MethodKey>
    {
        public readonly int OverloadIndex;
        public readonly GenericKey GenericKey;
        internal MethodKey(int index, GenericKey key)
        {
            OverloadIndex = index;
            GenericKey = key;
        }

        internal static MethodKey MethodQuery(MethodString mthdString)
        {
            string name = mthdString.NameOrValue;
            int split = name.IndexOf(':');
            //  string[] query = name.Split(':');
            int index = 0;
            if (split >= 0)
            {
                string indexed = name.Substring(split + 1); //stringSplit better here? prob doestn amtter
                index = int.Parse(indexed);
                name = name.Remove(split);
            }
            GenericKey key = new(name, mthdString.Generics.Count);
            return new(index, key);
        }

        bool IEquatable<MethodKey>.Equals(MethodKey obj)
        {
            return Equals(obj);
        }
        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 31 + OverloadIndex.GetHashCode();
                hash = hash * 31 + GenericKey.GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            StringBuilder sb = new(GenericKey.Key);
            if (OverloadIndex > 0)
                sb.Append($":{OverloadIndex}");
            return GenericKey.Args == 0 ? sb.ToString() : GenericKey.Args == 1 ? sb.Append("<T>").ToString() : GenericKey.ArgsToString(sb);
        }
        public bool Equals(MethodKey overload)
        {
            return overload.OverloadIndex == OverloadIndex && overload.GenericKey == GenericKey;
        }

        public override bool Equals(object? obj)
        {
            return obj is MethodKey overload && Equals(overload);
        } //obj is Overload overload && ((IEquatable<Overload>)this).Equals(overload);

        public static bool operator ==(MethodKey left, MethodKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MethodKey left, MethodKey right)
        {
            return !(left == right);
        }
    }

    internal static class TypeMap
    {

        internal static void MapType(Dictionary<MethodKey, MethodBase>? _methods, Dictionary<string, FieldInfo>? _fields, Type type, Dictionary<string, PropertyInfo>? _props, bool @new = true)
        {

            if (_methods != null)
            {
                _methods.Clear();
                List<MethodBase> methods = GetUnfilteredMethods(type, @new);
                List<MethodBase> filteredMethods = FilterSupportedMethods(methods).ToList();
                GenericKey[] distinctKeys = filteredMethods.Select(MethodGenericKey).Distinct().ToArray();
                foreach (GenericKey key in distinctKeys)
                    SortMethods(filteredMethods, key, _methods);
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

        static IEnumerable<MethodBase> FilterSupportedMethods(IEnumerable<MethodBase> methods)
        {
            foreach (MethodBase method in methods)
            {
                if (method.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                {
                    if (Reflector.SupportedMember(method, method.GetParameters()))
                        yield return method;
                }
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
            int count = 0;
            for (int i = 0; i < methods.Count; i++) 
            {
                MethodBase method = methods[i];
                GenericKey evaluatedKey = MethodGenericKey(method);
                if (evaluatedKey == key)
                {
                    MethodKey methodKey = new(count, evaluatedKey);
                    _methods[methodKey] = method;
                    methods.Remove(method);
                    i--;
                    count++;
                }

            }

        }

        internal static GenericKey MethodGenericKey(MethodBase method)
        {
            string evaluatedName = method is ConstructorInfo ? "new" : method.Name;//method.Name == ".ctor" ? "new" : method.Name;
            GenericKey evaluatedKey = new(evaluatedName, method);
            return evaluatedKey;
        }


    }
}