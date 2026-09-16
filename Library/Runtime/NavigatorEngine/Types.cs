using System.Reflection;
using System;
using System.Collections.Generic;
using XQuinn.Extensions;
using XQuinn.Runtime;
using XQuinn.Runtime.NavigatorEngine;
using XQuinn.Reflection;
using static XQuinn.Reflection.TypeCache;

namespace XQuinn.Runtime.NavigatorEngine
{


    static class Types
    {
        public static IEnumerable<string> Props<T>(string? contains = null) => Props(typeof(T), contains);
        public static IEnumerable<string> Methods<T>(string? contains = null, BindingFlags search = NavigatorCore.Flag) => Methods(typeof(T), contains, search);
        public static IEnumerable<string> Fields<T>(string? contains = null, BindingFlags search = NavigatorCore.Flag) => Fields(typeof(T), contains, search);
        public static IEnumerable<string> Props(Type t, string? contains = null)
        {
            Dictionary<string, PropertyInfo> props = new();
            TypeMap.MapType(null, null, t, props, false);
            return ReadMembers(props, contains, NavigatorCore.Flag);
        }

        public static IEnumerable<string> Fields(Type t, string? contains = null, BindingFlags search = NavigatorCore.Flag)
        {
            Dictionary<string, FieldInfo> fields = new();
            TypeMap.MapType(null, fields, t, null, false);
            return ReadMembers(fields, contains, search);
        }

        public static IEnumerable<string> Methods(Type t, string? contains = null, BindingFlags search = NavigatorCore.Flag)
        {
            Dictionary<MethodKey, MethodBase> methods = new();
            TypeMap.MapType(methods, null, t, null, contains?.EqualsCaseless("new") ?? true);
            return ReadMembers(methods, contains, search);
        }


        public static void FlushStaticCache(bool ambiguousMatches, bool accessedMembers, bool reifiedGenerics)
        {
            NavigatorCore.FlushStaticCache(ambiguousMatches, accessedMembers, reifiedGenerics);
        }

        public static Type Of<T>() => typeof(T); //typeof(T[])
        public static Type Of(string name) => GetTypeOrThrow(name); //for generic definitions which cant be passed as T without their own typeargs
        public static T Struct<T>(T obj = default) where T : unmanaged => obj;
        public static T Enum<T>(T obj) where T : Enum => obj;
        public static string String(string txt) => txt; //only way to instantiate an isolated new string using the navigator
        public static T[] Array<T>(params T[] arr) => arr.Length == 0 ? System.Array.Empty<T>() : arr;
        public static T[] Array<T>(int i) => i == 0 ? System.Array.Empty<T>() : new T[i];

        public static bool LateCache(string assemblyName, string targetTypeName, string keyForCaching)
        {
            Assembly assembly = Assembly.Load(assemblyName);
            return LateCache(assembly, targetTypeName, keyForCaching);

        }
        public static bool LateCache(Assembly assembly, string targetTypeName, string keyForCaching)
        {
            Type targetType = assembly.GetType(targetTypeName, false, true) ?? throw new ArgumentException($"No type found in assembly {assembly.FullName} named {targetTypeName}. Requires full name.");
            return CacheType(targetType, keyForCaching);
        }

        internal static IEnumerable<string> ReadMembers<K, V>(IEnumerable<KeyValuePair<K, V>> members, string? key, BindingFlags flags) where V : MemberInfo
        {
            if (key != null)
            {
                bool contained = false;
                foreach (KeyValuePair<K, V> member in members)
                {
                    if (SearchModifiers.ProcessSearch(member.Value, flags) && member.Key!.ToString()!.ContainsCaseless(key))
                    {
                        contained = true;
                        yield return $"[Key: {member.Key} :: {ReflectionPrinter.Print(member.Value, false)}]";
                    }
                }
                if (!contained)
                    yield return $"No {typeof(V).Name} found with name containing {key} with search option {flags}.";
            }
            else
                foreach (KeyValuePair<K, V> member in members)
                    if (SearchModifiers.ProcessSearch(member.Value, flags))
                        yield return $"[Key: {member.Key} :: {ReflectionPrinter.Print(member.Value, false)}]";
        }

        readonly struct SearchModifiers
        {
            readonly bool _static;
            readonly bool _public;
            readonly bool _inherited;

            public static bool ProcessSearch(MemberInfo inf, BindingFlags flags)
            {
                if (inf is MethodBase mthd)
                    return new SearchModifiers(mthd).ProcessSearch(flags);
                if (inf is FieldInfo field)
                    return new SearchModifiers(field).ProcessSearch(flags);
                return true;
            }

            public readonly bool ProcessSearch(BindingFlags flags)
            {
                if (_inherited)
                {
                    if (flags.HasFlag(BindingFlags.DeclaredOnly))
                        return false;
                }
                if (_public)
                {
                    if (!flags.HasFlag(BindingFlags.Public))
                        return false;

                }
                else
                {
                    if (!flags.HasFlag(BindingFlags.NonPublic))
                        return false;
                }
                if (_static && _inherited)
                    return flags.HasFlag(BindingFlags.FlattenHierarchy);
                if (_static)
                    return flags.HasFlag(BindingFlags.Static);
                else
                    return flags.HasFlag(BindingFlags.Instance);
            }

            public SearchModifiers(MethodBase method)
            {
                _static = method.IsStatic;
                _public = method.IsPublic;
                _inherited = Inherited(method);
            }

            public SearchModifiers(FieldInfo field)
            {
                _static = field.IsStatic;
                _public = field.IsPublic;
                _inherited = Inherited(field);
            }

            //                  public SearchModifiers(PropertyInfo prop)
            //                 {
            //                     _static = prop.GetGetMethod(true) is {IsStatic:true};
            //                     _inherited = Inherited(prop);
            //                 }
            static bool Inherited(MemberInfo obj)
            {
                if (obj.DeclaringType != null)
                    return obj.ReflectedType != obj.DeclaringType;
                return false;
            }

        }

    }
}
