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

namespace XQuinn.Private
{


    static class Program
    {

        static string path = string.Empty;
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
            path = XQuinn.IO.Finders.CodeLabFinder.Path;
            path = Path.Combine(path, @"XQuinnLib\dump\instanceread.log");
            // string command = $"~ *program.path; +var_path; *instancereader.new(var_path, true, types.of<object>()); +var_reader";
            Monitor monitor = new(true);
            //monitor._navigator.Interface(command);
            //InstanceReader reader = (InstanceReader)monitor._navigator._variables["var_reader"].Object;
            object[] arr = new object[] { 22, "hello world" };
            object ret = Activator.CreateInstance(typeof(@class), arr) ?? throw new();
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
                        Console.WriteLine(monitor.TryCatchInterface(msg, out _, out _));
                }
            }
#endif
        }

    }

    class BaseObj
    {
        public int Base_Object_Field;

        public static int Base_Static_Field;

        public void Base_Method()
        {}

        public static void Base_Static_Method()
        {
            
        }

        protected void ProtMethod()
        {
            
        }
    }
    class Obj : BaseObj
    {
        public void My_Method(){}


    }

    class @class
    {

        int field = 15;

        public string Str = "hi";

        public static string p(string s) => s;
        static bool unbox(int i, int x)
        {
            return ReferenceEquals(i, x);
        }
        static string Func() => "Executed";

        static string Func<T>() => "Executed<>";

        static string prms(int x, int y, params object[] arr)
        {
            StringBuilder sb = new();
            sb.Append(arr.Length);
            sb.AppendMany(arr, Environment.NewLine);
            return sb.ToString();
          //  return new StringBuilder().AppendMany(arr).ToString();
        }

        /// <summary>
        ///these were for generic cache testing
        /// </summary>
        /// <returns></returns>


        static string mthd() => "mthd";

        public @class() { }

        public @class(int i, string z) { field = i; Str = z;}
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