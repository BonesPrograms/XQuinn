#if !RELEASE_BUILD
using System.Collections.Concurrent;
using System.Reflection;
using XQuinn.Runtime;
using XQuinn.Reflection;
using XQuinn;
using XQuinn.NetConsole;
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

namespace _xquinn_prgrm
{


    static class _xquinn_prgrm_Main
    {
        static void Main(string[] args)
        {
#if IAPP_BUILD
            XQuinn.NetConsole.Apps.IApp.RunApp(args);
#elif DEBUG_BUILD
            IEnumerable<Type>? types = typeof(_xquinn_prgrm_Main).Module.GetTypes();
            TypeBook? book = TypeBook.New(types, x =>
            {
#if NET6_0_OR_GREATER
                ReadOnlySpan<char> z = x.IsGenericType ? x.SnipGenericName(false) : x.Name.AsSpan();
                if(z.SequenceEqual("GenericString") || z.SequenceEqual("TestingType")) return null;
                return TypeCache.GetCompatibleName(x, false);
#else
                string z = TypeCache.GetCompatibleName(x,false);
                if(z=="GenericString" || z=="TestingType")return null;
                return TypeCache.GetCompatibleName(x,false);
#endif
            }, StringComparer.OrdinalIgnoreCase);
            ConsoleTools.WriteMany(book, Environment.NewLine);
            TypeCache.CacheTypes(book);
            TypeCache.CacheType("Console", typeof(Console));
            TypeCache.CacheType("consoletools", typeof(ConsoleTools));
            Console.WriteLine(typeof(int[]));
            TestingType.Test<RuntimeNavigator>();

#endif


        }

    }
}

namespace XQ
{
    public static class Gen<T,X>
    {

        
    }
    public static class Test
    {
        public static (T1,T2) Get<T1,T2>() where T1: new() where T2: new() => new(new(),new());

        public static string String(string txt) => txt;
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