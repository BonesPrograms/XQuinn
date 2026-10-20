using System.Reflection;
using XQuinn.LangInterp.SyntaxTree;
using System;
using System.Collections.Generic;

namespace XQuinn.Runtime.NavigatorEngine
{
    static class RuntimeCache
    {
        internal static Dictionary<Type, HashSet<string>> AmbiguousMatches { get; } = new();
        internal static Dictionary<Type, Dictionary<string, MemberInfo>> GlobalMetadata { get; } = new();
        internal static Dictionary<string, Type> ReifiedGenericTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal static bool CheckAmbiguousMatch(Type fromType, MethodString mthdString, out Type cachedType, out HashSet<string>? cachedMatches)
        {
            cachedMatches = null;
            cachedType = fromType.IsGenericType && !fromType.IsGenericTypeDefinition ? fromType.GetGenericTypeDefinition() : fromType;
            AmbiguousMatches.TryGetValue(cachedType, out cachedMatches);
            return cachedMatches?.Contains(mthdString.Name) ?? false;
        }

        internal static void FlushStaticCache()
        {
            AmbiguousMatches.Clear();
            GlobalMetadata.Clear();
            ReifiedGenericTypes.Clear();
        }

        internal static T? FromCache<T>(string key, Type fromType, out bool typeCached, out bool memberCached) where T : MemberInfo
        {
            memberCached = false;
            typeCached = GlobalMetadata.TryGetValue(fromType, out Dictionary<string, MemberInfo>? cachedMembers);
            if (typeCached)
            {
                memberCached = cachedMembers!.TryGetValue(key, out MemberInfo? member);
                if (memberCached)
                    return (T)member!;
            }
            return null;
        }

        internal static void CacheAmbiguousMatch(HashSet<string>? cachedMatches, Type cachedType, MethodString mthdString)
        {
            if (cachedMatches == null)
            {
                cachedMatches = new(StringComparer.OrdinalIgnoreCase);
                AmbiguousMatches[cachedType] = cachedMatches;
            }
            cachedMatches.Add(mthdString.Name);
        }

        internal static void CacheMember(bool typeCached, bool memberCached, Type fromType, MemberInfo member, string key)
        {
            if (memberCached)
                return;
            Dictionary<string, MemberInfo> cachedMembers;
            if (typeCached)
                cachedMembers = GlobalMetadata[fromType];
            else
            {
                cachedMembers = new(StringComparer.OrdinalIgnoreCase);
                GlobalMetadata[fromType] = cachedMembers;
            }
            cachedMembers[key] = member;
        }
    }
}
