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
using XQuinn.LexicalAnalysis;
using System.Linq;
using System.Collections.Generic;
using XQuinn.Extensions;
using XQuinn.Parsing;
using System.IO;
using XQuinn.LexicalAnalysis.Syntaxes;
using System.Collections;
using XQuinn.ObjectModel;
using System.Runtime.InteropServices;

namespace XQuinn.Private
{


    static class Program
    {
        static Monitor s_monitor = new();
        static string s_path = string.Empty;
        static Span<char> span(string x)
        {
            Span<char> span = new Span<char>(x.ToArray());
            return span;
        }

        static void Main(string[] args)
        {
#if IAPP_BUILD
            XQuinn.NetConsole.Apps.IApp.RunApp(args);
#elif DEBUG_BUILD
            Assembly xquinn = Assembly.Load("XQuinn");
            TypeCache.CacheTypes(xquinn.GetTypes(), false);
            TypeCache.CacheType<Harmony>(false);
            TypeCache.CacheType(typeof(AccessTools), false);
            TypeCache.CacheType("accesstoolsE", typeof(AccessToolsExtensions));
            //  TypeCache.CacheType(typeof(Program), false);
            //    TypeCache.CacheType("span", typeof(MemoryExtensions));
            s_path = XQuinn.IO.Finders.CodeLabFinder.s_path;
            s_path = Path.Combine(s_path, @"XQuinnLib\dump\instanceread.log");
            // string command = $"~ *program.path; +var_path; *instancereader.new(var_path, true, types.of<object>()); +var_reader";
            //Monitor monitor = new(true);
            //monitor._navigator.Interface(command);
            //InstanceReader reader = (InstanceReader)monitor._navigator._variables["var_reader"].Object;

            while (true)
            {
                // var key = Console.ReadKey();
                // if (key.Key == ConsoleKey.Escape)
                // {

                //     reader.Dispose();
                //     return;
                // }
                string? msg = Console.ReadLine();
                if (msg != null)
                {
                    //msg = $"{key.KeyChar}{msg}";
                    if (msg.EqualsCaseless("exit"))// || string.IsNullOrWhiteSpace(msg))
                    {
                        //  reader.Dispose();
                        return;
                    }
                    else
                        Console.WriteLine(s_monitor.SafeInterface(msg, out _, out _));
                }
            }
#endif
        }

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

    }


    class Class<T>
    {
        static T[] arr = Array.Empty<T>();
        static T? obj = default;

        static T Ret(T obj) => obj;
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