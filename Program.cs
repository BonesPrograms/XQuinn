#if !RELEASE_BUILD
using System.Collections.Concurrent;
using System.Reflection;
using XQuinn.Runtime;
using XQuinn.Reflection;
using XQuinn;
using HarmonyLib;
using XQuinn.IO;
using System.Text;
using System;
using System.Runtime.Versioning;
using XQuinn.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using XQuinn.Extensions;
using XQuinn.Parsing;
using System.IO;
using XQuinn.CodeAnalysis.AST;
using System.Collections;
using XQuinn.ObjectModel;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace XQuinn.Private
{


    static class Program
    {


        static void Main(string[] args)
        {
#if IAPP_BUILD
            XQuinn.NetConsole.Apps.IApp.RunApp(args);
#elif DEBUG_BUILD
            Cache();
            RunNavigator();
#endif
        }
#if DEBUG_BUILD


        static class BitConv
        {

        }

        static void RunNavigator()
        {
            Monitor monitor = new();
            while (true)
            {
                string? msg = Console.ReadLine();
                if (msg != null)
                    Console.WriteLine(monitor.SafeInterface(msg, out _, out _));
            }
        }
        static void Cache()
        {
            Assembly xquinn = Assembly.Load("XQuinn");
            TypeCache.CacheTypes(xquinn.GetTypes(), false);
            TypeCache.CacheType<Harmony>(false);
            TypeCache.CacheType(typeof(AccessTools), false);
            TypeCache.CacheType(typeof(AccessToolsExtensions), "accesstoolsE");
            TypeCache.CacheType(typeof(BitConverter), false);
            //@ TypeCache.CacheType(typeof(BytesLittleEndian), "bytes");
        }
#endif





    }




    abstract class BaseClass
    {
        public static T Obj<T>(T val) => val;

        public static T[] Prms<T>(params T[] prms) => prms;
    }

    class Static : BaseClass
    {

    }

    class Instance : BaseClass
    {

        int a;
        public bool Method(Instance x)
        {
            return x is Instance;
        }
    }


    class Class<T> where T : new()
    {
        static T[] arr = Array.Empty<T>();
        static T? obj = default;

        static T Ret(T obj) => obj;

        static IReadOnlyList<T> list = new List<T>()
        {
            new(), new(), new()
        };
    }

    class Test
    {
        Dictionary<GenericKey,int> types = new();

        public void Run()
        {
            GenericKey key = new("class", 1);
            types[key] = 0;


            TypeString str = TypeString.New("class<int>");
            GenericKey query = new(str);

            Console.WriteLine(types[query]);
        }
    }

    internal readonly struct GenericKey : IEquatable<GenericKey>
    {

        public readonly string Name;

        public readonly int GenericArgs;

        public GenericKey(string key, int args)
        {
            Name = key;
            GenericArgs = args;
        }

        public GenericKey(TypeString str)
        {
            Name = str.NameOrValue;
            GenericArgs = str.Generics.Count;
        }

        public override bool Equals(object obj)
        {
            return obj is GenericKey key && Equals(key);
        }

        public bool Equals(GenericKey key)
        {
            return key.GenericArgs == GenericArgs && key.Name.EqualsCaseless(Name);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + StringComparer.OrdinalIgnoreCase.GetHashCode(Name);//StringComparer.FromComparison(StringComparison.OrdinalIgnoreCase).GetHashCode(KeySpan);
                hash = hash * 23 + GenericArgs.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(GenericKey left, GenericKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GenericKey left, GenericKey right)
        {
            return !(left == right);
        }



    }


}


// public class Lex
// {

//     static string Num;

//     public static void Char(char i, char d)
//     {
//         Console.WriteLine(i);
//     }
//     public static void Call(string i, string x, bool value)
//     {
//         Console.WriteLine(x);
//     }

//     static char Oth(char x, char y)
//     {
//         Console.WriteLine($"Oth invoked with {x} and {y}");
//         return y;
//     }
//     static Char Get(char i) => 'c';

//     static char sex(char i, char x) => 'd';
// }


// //char(lex.get(lex.oth('x', lex.get('y'))), lex.get(lex.get(lex.get(lex.oth('x',lex.get('y')))))
#endif