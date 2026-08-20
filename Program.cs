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
                if (x.Name.StartsWith('_')) return null;
                string z = x.SnipGenericShortName();
                return z == "GenericString" || z == "TestingType" ? null : z;
            }, StringComparer.OrdinalIgnoreCase);
            //RuntimeCommand.Register(types, "XQuinn");
            TypeCache.CacheTypes(book);
            types = null;
            book = null;
            TypeCache.CacheType("Console", typeof(Console));
            TestingType.Test<CallInterpreter>();
           
#endif


        }

    }



    public class Fun
    {
        public static void OX()
        {
            
        }

        public static void OX(int i)
        {
            
        }

        public static void OX(string x)
        {
            
        }
        public static string Params<T>(params T[] prms)
        {
            
            StringBuilder sb = new();
            sb.AppendMany(prms);
            return sb.ToString();
        }
        public static string Params(string f, bool y, params int[] prms)
        {
            StringBuilder sb = new();
            sb.AppendMany(prms);
            return sb.ToString();
        }
        public static int Method(int i, char z, string? x = null) => i;
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

}
// //char(lex.get(lex.oth('x', lex.get('y'))), lex.get(lex.get(lex.get(lex.oth('x',lex.get('y')))))
#endif