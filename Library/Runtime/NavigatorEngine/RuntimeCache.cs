using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Reflection;
using XQuinn.CodeAnalysis.AST;
using System.Linq;
using System;
using System.Collections.Generic;
using XQuinn.CodeAnalysis;
using System.Text;
using System.Collections;
using System.Runtime.ExceptionServices;
using XQuinn.Runtime;

namespace XQuinn.Runtime.NavigatorEngine
{
    

        static class RuntimeCache
        {
            internal static readonly Dictionary<Type, HashSet<string>> s_ambiguous_matches = new();
            internal static readonly Dictionary<Type, Dictionary<string, MemberInfo>> s_known_members = new(); //all members ever accessed by callinterp 
            internal static readonly Dictionary<string, Type> s_reified_generic_types = new(StringComparer.OrdinalIgnoreCase); //reified generics

            internal static bool CheckAmbiguousMatch(Type fromType, MethodString mthdString, out Type cachedType, out HashSet<string>? cachedMatches)
            {
                cachedMatches = null;
                cachedType = fromType.IsGenericType && !fromType.IsGenericTypeDefinition ? fromType.GetGenericTypeDefinition() : fromType;
                s_ambiguous_matches.TryGetValue(cachedType, out cachedMatches);
                return cachedMatches?.Contains(mthdString.Name) ?? false;
            }

            public static void FlushStaticCache(bool ambiguousMatches = false, bool typeMembers = true, bool reifiedGenerics = true)
            {
                if (ambiguousMatches)
                    s_ambiguous_matches.Clear(); //technically not part of 'caching' system, doesnt care if caching is false, will always store ambiguous matches
                if (typeMembers)                    //may be a thing later, for now you can clear at least
                    s_known_members.Clear();
                if (reifiedGenerics)
                    s_reified_generic_types.Clear();
            }

            internal static T? FromCache<T>(string key, Type fromType, out bool typeCached, out bool memberCached) where T : MemberInfo
            {
                memberCached = false;
                typeCached = s_known_members.TryGetValue(fromType, out Dictionary<string,MemberInfo>? cachedMembers);
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
                    s_ambiguous_matches[cachedType] = cachedMatches;
                }
                cachedMatches.Add(mthdString.Name);
            }

            internal static void CacheMember(bool typeCached, bool memberCached, Type fromType, MemberInfo member, string key) //ResolvedOverload? overloadKey)
            {

                if (memberCached)
                    return;
                Dictionary<string, MemberInfo> cachedMembers;
                if (typeCached)
                    cachedMembers = s_known_members[fromType];
                else
                {
                    cachedMembers = new(StringComparer.OrdinalIgnoreCase);
                    s_known_members[fromType] = cachedMembers;
                }
                cachedMembers[key] = member;
                // if (fromType == LoadedType)
                // {
                //     if (member is FieldInfo field) _fields.Remove(key);
                //     else if (member is MethodInfo mthd) { _methods.Remove(key);}
                //     if(overloadKey!=null) _overloads.Remove(overloadKey.Value);
                //     Console.WriteLine($"{overloadKey!=null} OVERLOADKEY REMOVED?");
                // }
            }
        }
    }

