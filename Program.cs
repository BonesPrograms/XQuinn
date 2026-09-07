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
using XQuinn.Private.EqualityHelpers;

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
            TypeCache.CacheType(typeof(TypeCache), false);
            //@ TypeCache.CacheType(typeof(BytesLittleEndian), "bytes");
        }
#endif





    }

    class Prop
    {
        public string Method() => "Invoked";

        public Prop This() => this;
    }


    class Class<T> where T : new()
    {

        public static T? Obj
        {
            get=>_obj;
            set=>_obj=value;
        }
        static T? _obj = new();

        public static T Method(T obj) => obj;

        public static void Test(char x, char y) {}

        public static int Method(int i) => i;
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