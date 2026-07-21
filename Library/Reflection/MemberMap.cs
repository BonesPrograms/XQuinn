using System.Reflection;
using XQuinn.Reflection;
using XQuinn.Extensions;
using System.Collections.Immutable;
using static XQuinn.Reflection.MemberGroup;
using System.Collections.Concurrent;
namespace XQuinn.Reflection;



/// //need to add property and event privacy support for basetypes. well the methods will still show up actually now that i think about it

public sealed class MemberMap : IEnumerable<MemberInfo>
{
    public readonly bool StaticOnly;
    public readonly Type Type;
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

    public static MemberMap New(Type type, bool declaredOnly, bool staticOnly, bool traverseBaseForPrivate, MemberGroup member = MemberGroup.All, Func<MemberInfo, bool>? filter = null)
    {
        BindingFlags flag = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        flag |= declaredOnly ? BindingFlags.DeclaredOnly : BindingFlags.FlattenHierarchy;
        if (!staticOnly)
            flag |= BindingFlags.Instance;
        Dictionary<MemberGroup, List<MemberInfo>> members = [];
        AddMembers(type, flag, member, members, filter);
        if (traverseBaseForPrivate)
            BaseTypeTraversal(type, staticOnly, member, members, filter);
        foreach (var obj in members)
        {
            obj.Value.RemoveAll(x => x.DeclaringType == typeof(object));
        }
        return new(type, members, staticOnly);
    }


    public bool ContainsKey(MemberGroup group) => Map.ContainsKey(group);
    public bool TryGetValue(MemberGroup key, out List<MemberInfo>? list) => Map.TryGetValue(key, out list);

    public int? MemberCount(MemberGroup key)
    {
        if (TryGetValue(key, out List<MemberInfo>? list))
            return list!.Count;
        return null;
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
    public Dictionary<string, MethodInfo>? MapOverloads(out List<MemberInfo>? methods, StringComparer comp)
    {
        methods = CopyMethods();
        string[] names = methods.Select(x => x!.Name).Distinct(comp).ToArray();
        Dictionary<string, MethodInfo>? overloads = MapOverloads(names, methods, comp);
        if (overloads == null)
            methods = null;
        return overloads;
    }

    public MemberMap Copy(Func<MemberInfo, bool>? filter = null)
    {
        Dictionary<MemberGroup, List<MemberInfo>> dic = [];
        foreach (var pair in Map)
        {
            List<MemberInfo>? transfer = null;
            if (filter != null)
            {
                var filtered = pair.Value.Where(filter);
                if (filtered.Any())
                    transfer = filtered.ToList();
            }
            else
                transfer = new(pair.Value);
            if (transfer?.Count > 0)
            {
                dic[pair.Key] = transfer;
            }
        }
        return new(Type, dic, StaticOnly);
    }

    public List<MemberInfo> CopyMethods()
    {
        List<MemberInfo>? methods = null;
        if (TryGetValue(Method, out List<MemberInfo>? list))
            methods = new(list!);
        if (methods == null)
            throw new InvalidOperationException("Member map does not have any methods stored.");
        return methods;
    }
    static Dictionary<string, MethodInfo>? MapOverloads(string[] names, List<MemberInfo> methods, StringComparer comp)
    {
        Dictionary<string, MethodInfo>? overloads = null;
        foreach (var name in names)
        {
            int count = 0;
            MethodInfo? first = null;
            CompareNames(ref count, methods, ref overloads, ref first, name, comp);
            if (count > 1)
                methods.RemoveAll(x => x.Name.EqualsCaseless(name));
        }
        return overloads;
    }



    static void CompareNames(ref int count, List<MemberInfo> methods, ref Dictionary<string, MethodInfo>? overloads, ref MethodInfo? first, string name, StringComparer comp)
    {
        foreach (var method in methods)
        {
            if (name.EqualsCaseless(method.Name))
            {
                count++;
                if (count > 1)
                {
                    overloads ??= new(comp);
                    AddOverload(count, ref first, name, comp, method, overloads);
                }
                else if (count == 1)
                    first = (MethodInfo?)method;
            }
        }
    }


    static void AddOverload(int count, ref MethodInfo? first, string name, StringComparer comp, MemberInfo method, Dictionary<string, MethodInfo> overloads)
    {
        if (first != null)
        {
            overloads[$"{name}1"] = first;
            first = null;
        }
        overloads[$"{name}{count}"] = (MethodInfo)method;
    }



    static List<MemberInfo>? GetMember<T>(T[] main, Func<MemberInfo, bool>? filter, bool priv) where T : MemberInfo
    {
        IEnumerable<MemberInfo> enumer;
        if (filter != null)
            enumer = main.Where(filter);
        else
            enumer = main;
        if (priv)
        {
            if (typeof(T).IsAssignableTo(typeof(MethodBase)))
            {
                enumer = enumer.Where(x => x is MethodBase m && m.IsPrivate);
            }
            else if (typeof(T) == typeof(FieldInfo))
            {
                enumer = enumer.Where(x => x is FieldInfo f && f.IsPrivate);
            }
        }
        if (enumer.Any())
            return [.. enumer];
        return null;

    }
    static void AddBasePrivates<T>(MemberGroup flagcheck, MemberGroup member, Dictionary<MemberGroup, List<MemberInfo>> members, Func<MemberInfo, bool>? filter, T[] arr) where T : MemberInfo
    {
        if (member.HasFlag(flagcheck))
        {
            if (members.TryGetValue(flagcheck, out List<MemberInfo>? list))
            {
                var reflections = GetMember<T>(arr, filter, true);
                if (reflections != null)
                    list.AddRange(reflections);
            }
            else
            {
                var reflections = GetMember(arr, filter, true);
                if (reflections != null)
                    members[member] = reflections;
            }
        }
    }

    static void BaseTypeTraversal(Type type, bool staticOnly, MemberGroup member, Dictionary<MemberGroup, List<MemberInfo>> members, Func<MemberInfo,bool>? filter)
    {
        Type? baseType = type.BaseType;
        BindingFlags flag = BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Static;
        if (!staticOnly)
            flag |= BindingFlags.Instance;
        while (baseType != null && baseType != typeof(object))
        {
            AddBasePrivates(Field, member, members, filter, baseType.GetFields(flag));
            AddBasePrivates(Method, member, members, filter, baseType.GetMethods(flag));
            //     AddBasePrivates(Property, member, members, filter, baseType.GetProperties(flag));
            //      AddBasePrivates(Event, member, members, filter, baseType.GetEvents(flag));
            AddBasePrivates(Constructor, member, members, filter, baseType.GetConstructors(flag));
            baseType = baseType.BaseType;
        }
    }


    static void AddMembers(Type type, BindingFlags flag, MemberGroup member, Dictionary<MemberGroup, List<MemberInfo>> members, Func<MemberInfo, bool>? filter)
    {
        if (member.HasFlag(Field))
        {
            var fields = GetMember(type.GetFields(flag), filter, false);
            if (fields != null)
                members[Field] = fields;
        }

        if (member.HasFlag(Method))
        {
            var methods = GetMember(type.GetMethods(flag), filter, false);
            if (methods != null)
                members[Method] = methods;
        }

        if (member.HasFlag(Property))
        {
            var props = GetMember(type.GetProperties(flag), filter, false);
            if (props != null)
                members[Property] = props;
        }

        if (member.HasFlag(Event))
        {
            var events = GetMember(type.GetEvents(flag), filter, false);
            if (events != null)
                members[Event] = events;
        }
        if (member.HasFlag(Constructor))
        {
            var ctors = GetMember(type.GetConstructors(flag), filter, false);
            if (ctors != null)
                members[Constructor] = ctors;
        }

    }
    //static IEnumerable<T> Filter<T>(T[] arr, Func<MemberInfo, bool>? filter) where T : MemberInfo => filter == null ? arr : arr.Where(x => filter(x));
    // static IEnumerable<T> PrivateMethod<T>(IEnumerable<T> objs) where T : MethodBase => objs.Where(x => x.IsPrivate);

    // static List<MemberInfo>? MakeList(IEnumerable<MemberInfo> enumer)
    // {
    //     if (enumer.Any())
    //         return [.. enumer];
    //     return null;
    // }
    // static List<MemberInfo>? GetFields(Type type, BindingFlags flag, Func<MemberInfo, bool>? filter, bool priv)
    // {
    //     var enumer = Filter(type.GetFields(flag), filter);
    //     if (priv)
    //         enumer = enumer.Where(x => x.IsPrivate);
    //     return MakeList(enumer);
    // }


    // static List<MemberInfo>? GetMethods(Type type, BindingFlags flag, Func<MemberInfo, bool>? filter, bool priv)
    // {
    //     var enumer = Filter(type.GetMethods(flag), filter);
    //     enumer = PrivateMethod(enumer, priv);
    //     return MakeList(enumer);
    // }

    // static List<MemberInfo>? GetEvents(Type type, BindingFlags flag, Func<MemberInfo, bool>? filter, bool priv)
    // {
    //     var enumer = Filter(type.GetProperties(flag), filter);
    //     //  if (priv)
    //     // enumer = enumer.Where(x => x.IsPrivate);
    //     return MakeList(enumer);
    // }

    // static List<MemberInfo>? GetProperties(Type type, BindingFlags flag, Func<MemberInfo, bool>? filter, bool priv)
    // {
    //     var enumer = Filter(type.GetEvents(flag), filter);
    //     // if (priv)
    //     //   enumer = enumer.Where(x => x.IsPrivate);
    //     return MakeList(enumer);
    // }

    // static List<MemberInfo>? GetConstructors(Type type, BindingFlags flag, Func<MemberInfo, bool>? filter, bool priv)
    // {
    //     var enumer = Filter(type.GetConstructors(flag), filter);
    //     enumer = PrivateMethod(enumer, priv);
    //     return MakeList(enumer);
    // }




    //this can maybe be some kind of base method

    //hash set doesnt seem like it would work here though
    //the issue we were having was duplicate values in a list represented by a single key though...
    //so i think a hash set would work here probably

    //resorts the list into a dictionary, keyed by ReflectionGroup, all objects of a particular ReflectionGroup are grouped together in a list
    // static void ResolveByMember(ReflectionGroup member, List<MemberInfo> info, Dictionary<ReflectionGroup, List<MemberInfo>> members)
    // {
    //     IEnumerable<MemberInfo> specifiedmembers = info.Where(x => member == x.Group);
    //     //  specifiedmembers.ForEach(x => info.Remove(x));
    //     // foreach (var obj in info.ToArray())
    //     // {
    //     //     if (member.HasFlag(obj.ReflectionGroup))
    //     //     {
    //     //         specifiedmembers.Add(obj);
    //     //         info.Remove(obj);
    //     //     }
    //     // }
    //     if (specifiedmembers.Any())
    //         members[member] = [.. specifiedmembers];
    // }

    /// <summary>
    /// Will get all static, instance, public and nonpublic members, but only declared by the type (to prevent possible overlapping)
    /// </summary>
    /// <param name="type"></param>
    /// <param name="member"></param>
    /// <param name="filter"></param>
    /// <returns></returns>

    // static List<MemberInfo> ResolveTypeMembers(Type type, ReflectionGroup member = ReflectionGroup.All, Func<MemberInfo, bool>? filter = null)
    // {

    //     MemberInfo[] members = type.GetMembers(allDeclared);
    //     List<MemberInfo> typedata = new(members.Length);
    //     foreach (var membertype in ReflectionGroups.Groups)
    //     {
    //         if (member.HasFlag(membertype))
    //         {
    //             GetMetadataOf(typedata, members, membertype, filter);
    //         }
    //     }
    //     return typedata;
    // }

    // ///a hash set would be more efficient here and in TypeMap.ResolveByMember, but, using hash sets would
    // static void GetMetadataOf(List<MemberInfo> metadata, MemberInfo[] array, ReflectionGroup member, Func<MemberInfo, bool>? filter)
    // {
    //     List<MemberInfo> members = [.. array
    //     .Where(obj => !obj.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))
    //     && member == ReflectionGroups.GetGroup(obj)
    //     && (filter?.Invoke(obj) ?? true)).Select(x => new MemberInfo(x))];
    //     metadata.AddRange(members); //actually faster to allocate it to a list here for addrange purposes rather than sending an enumerable
    //     // foreach (I obj in array.ToArray())
    //     // {
    //     //     if ()
    //     //     {
    //     //         metadata.Add(new(obj));
    //     //         array.Remove(obj);
    //     //     }
    //     // }
    // }

}