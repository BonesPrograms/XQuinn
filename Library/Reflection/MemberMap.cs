using System;
using System.Reflection;
using XQ.Reflection;
using XQ.Extensions;
using static XQ.Reflection.MemberGroup;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
namespace XQ.Reflection
{



    //Must be power of 2 to work with flags... need to research why

    [Flags] //flags are used for querying, so that you can get members of multiple different kinds from my reflection collections via enum input
    public enum MemberGroup //each reflectioninfo object will only have one flag
    {
        _invalid = 0,
        Field = 8,
        Property = 16,
        Method = 32,
        Event = 64,
        Constructor = 128, //why does it say all is already 127 if i make this 127.. .wait a minute... math... thats why...
        All = Property | Field | Constructor | Method | Event
    }

    /// //need to add property and event privacy support for basetypes. well the methods will still show up actually now that i think about it

    public sealed class MemberMap : IEnumerable<MemberInfo>
    {
        public readonly bool StaticOnly;
        public readonly System.Type Type;
        public readonly Dictionary<MemberGroup, List<MemberInfo>> Map;
        public int Count => Map.Count;
        public ICollection<MemberGroup> Groups => Map.Keys;
        public ICollection<List<MemberInfo>> Values => Map.Values;

        public List<MemberInfo> this[MemberGroup group]
        {
            get => Map[group];
            set => Map[group] = value;
        }

        MemberMap(Type type, Dictionary<MemberGroup, List<MemberInfo>> members, bool statics)
        {
            Map = members;
            Type = type;
            StaticOnly = statics;
        }

        public static MemberMap New(Type type, bool declaredOnly, bool staticOnly, bool getBasePrivateMembers, MemberGroup memberType = MemberGroup.All, Func<MemberInfo, bool>? filter = null, bool removeSystemObject = true)
        {
            BindingFlags flag = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            flag |= declaredOnly ? BindingFlags.DeclaredOnly : BindingFlags.FlattenHierarchy;
            if (!staticOnly)
                flag |= BindingFlags.Instance;
            Dictionary<MemberGroup, List<MemberInfo>> allMembers = new();
            AddMembers(type, flag, memberType, allMembers, filter);
            if (getBasePrivateMembers)
                BaseTypeTraversal(type, staticOnly, memberType, allMembers, filter);
            if (removeSystemObject)
                foreach (List<MemberInfo> membersOfKind in allMembers.Values)
                    membersOfKind.RemoveAll(x => x.DeclaringType == typeof(object));
            return new(type, allMembers, staticOnly);
        }


        public bool ContainsKey(MemberGroup group) => Map.ContainsKey(group);
        public bool TryGetValue(MemberGroup key,
#if NET6_0_OR_GREATER
        [NotNullWhen(true)]
        #endif
         out List<MemberInfo>? list) => Map.TryGetValue(key, out list);

        public int? MemberCount(MemberGroup key)
        {
            if (TryGetValue(key, out List<MemberInfo>? list))
                return list.Count;
            else return null;
        }
        public IEnumerator<MemberInfo> GetEnumerator()
        {
            foreach (var pair in Map)
                foreach (var obj in pair.Value)
                    yield return obj;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }



        /// <summary>
        /// Checks for overloads and maps them to a dictionary. Outs a list that has all overloaded methods removed. Returns null and outs null if there are no overloads.
        /// </summary>
        /// <param name="methods"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>


        public MemberMap Copy(Func<MemberInfo, bool>? filter = null)
        {
            Dictionary<MemberGroup, List<MemberInfo>> dic = new();
            foreach (var pair in Map)
            {
                List<MemberInfo>? transfer = null;
                if (filter != null)
                {
                    IEnumerable<MemberInfo> filtered = pair.Value.Where(filter);
                    if (filtered.Any())
                        transfer = filtered.ToList();
                }
                else
                    transfer = new(pair.Value);
                if (transfer?.Count > 0)
                    dic[pair.Key] = transfer;
            }
            return new(Type, dic, StaticOnly);
        }


        static List<MemberInfo>? GetMembers<T>(T[] members, Func<MemberInfo, bool>? filter, bool priv) where T : MemberInfo
        {
            IEnumerable<MemberInfo> filteredMembers = filter != null ? members.Where(filter) : members;
            if (priv)
                filteredMembers = EnumeratePrivateMembers(filteredMembers);
            return filteredMembers.Any() ? filteredMembers.ToList() : null;
        }

        static IEnumerable<MemberInfo> EnumeratePrivateMembers(IEnumerable<MemberInfo> filteredMembers)
        {
            foreach (var member in filteredMembers)
            {
                AccessModifiers modifiers = member is FieldInfo field ? new(field) : new((MethodBase)member);
                if (modifiers.IsPrivate)
                    yield return member;
            }
        }
        static void AddBasePrivates<T>(MemberGroup flagcheck, MemberGroup memberType, Dictionary<MemberGroup, List<MemberInfo>> allTypeMembers, Func<MemberInfo, bool>? filter, T[] arr) where T : MemberInfo
        {
            if (memberType.HasFlag(flagcheck))
            {
                if (allTypeMembers.TryGetValue(flagcheck, out List<MemberInfo>? list))
                {
                    List<MemberInfo>? retrievedMembers = GetMembers(arr, filter, true);
                    if (retrievedMembers != null)
                        list.AddRange(retrievedMembers);
                }
                else
                {
                    List<MemberInfo>? retrievedMembers = GetMembers(arr, filter, true);
                    if (retrievedMembers != null)
                        allTypeMembers[memberType] = retrievedMembers;
                }
            }
        }

        static void BaseTypeTraversal(Type type, bool staticOnly, MemberGroup memberType, Dictionary<MemberGroup, List<MemberInfo>> allTypeMembers, Func<MemberInfo, bool>? filter)
        {
            Type? baseType = type.BaseType;
            BindingFlags flag = BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Static;
            if (!staticOnly)
                flag |= BindingFlags.Instance;
            while (baseType != null && baseType != typeof(object))
            {
                AddBasePrivates(Field, memberType, allTypeMembers, filter, baseType.GetFields(flag));
                AddBasePrivates(Method, memberType, allTypeMembers, filter, baseType.GetMethods(flag));
                //     AddBasePrivates(Property, member, members, filter, baseType.GetProperties(flag));
                //      AddBasePrivates(Event, member, members, filter, baseType.GetEvents(flag));
                AddBasePrivates(Constructor, memberType, allTypeMembers, filter, baseType.GetConstructors(flag));
                baseType = baseType.BaseType;
            }
        }


        static void AddMembers(Type type, BindingFlags flag, MemberGroup memberType, Dictionary<MemberGroup, List<MemberInfo>> allTypeMembers, Func<MemberInfo, bool>? filter)
        {
            if (memberType.HasFlag(Field))
            {
                List<MemberInfo>? fields = GetMembers(type.GetFields(flag), filter, false);
                if (fields != null)
                    allTypeMembers[Field] = fields;
            }
            else if (memberType.HasFlag(Method))
            {
                List<MemberInfo>? methods = GetMembers(type.GetMethods(flag), filter, false);
                if (methods != null)
                    allTypeMembers[Method] = methods;
            }
            else if (memberType.HasFlag(Property))
            {
                List<MemberInfo>? props = GetMembers(type.GetProperties(flag), filter, false);
                if (props != null)
                    allTypeMembers[Property] = props;
            }
            else if (memberType.HasFlag(Event))
            {
                List<MemberInfo>? events = GetMembers(type.GetEvents(flag), filter, false);
                if (events != null)
                    allTypeMembers[Event] = events;
            }
            else if (memberType.HasFlag(Constructor))
            {
                List<MemberInfo>? ctors = GetMembers(type.GetConstructors(flag), filter, false);
                if (ctors != null)
                    allTypeMembers[Constructor] = ctors;
            }

        }

    }
}