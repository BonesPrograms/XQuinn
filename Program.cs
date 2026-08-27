#if !RELEASE_BUILD
using System.Collections.Concurrent;
using System.Reflection;
using XQ.Runtime;
using XQ.Reflection;
using XQ;
using XQ.NetConsole;
using HarmonyLib;
using XQ.IO;
using System.Text;
using System;
using System.Runtime.Versioning;
using XQ.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using XQ.Extensions;
using XQ.Parsing;
using System.IO;
using XQ.CodeAnalysis.AST;
using System.Collections;

namespace XQ
{


    static class Program
    {

        static readonly NavigationMonitor Monitor = new(true);

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
            IEnumerable<Type>? types = typeof(Program).Module.GetTypes();
            TypeBook book = TypeBook.New(types, x => TypeCache.GetCompatibleName(x, false), StringComparer.OrdinalIgnoreCase);
            TypeCache.CacheTypes(book);
            TypeCache.CacheType("consoletools", typeof(ConsoleTools));
            TypeCache.CacheType("console", typeof(Console));
            TypeCache.CacheType("harmony", typeof(Harmony));
            TypeCache.CacheType("span", typeof(MemoryExtensions));
            while (true)
            {
                string? msg = Console.ReadLine();
                if (msg != null)
                    if (msg.EqualsCaseless("exit"))
                        return;
                    else
                        Console.WriteLine(Monitor.TryCatchInterface(msg, out _, out _));
            }
#endif
        }

    }
    public class Basado
    {
        int F;
    }
    public class Ex : Basado
    {
        
        public static string Field = null;

        public static string flag(BindingFlags flag) => flag.ToString();
        public static string Str()=>string.Empty;
        public static void Int(object i)
        {
            inner((int)i);
        }

        public static string Str(object obj) => obj.ToString();

        static void inner(int i, int x = 2)
        {
            
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