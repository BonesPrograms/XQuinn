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

namespace _xquinn_prgrm
{


    static class _xquinn_prgrm_Main
    {
        static void Main(string[] args)
        {

#if NET6_0_OR_GREATER
            XQuinn.NetConsole.Apps.IApp.RunApp(args);
#endif
            IEnumerable<Type> types = typeof(_xquinn_prgrm_Main).Module.GetTypes().Where(x => x.Namespace == "_xquinn_prgrm");
            TypeBook book = TypeBook.New(types, x => x.SnipGenericShortName(), StringComparer.OrdinalIgnoreCase);
            TypeCache.CacheTypes(book);
            TypeCache.CacheType("Console", typeof(Console));
            //RuntimeCommand.Register(types, "XQuinn");
            Console.Clear();
            TestingType.Test<CallInterpreter>();


        }

    }

    class T
    {
        public static string Call(int x = -1, int z = -1, int y = -1)
        {
            Console.WriteLine("T.Call(intintint)");
            return $"{x} {z} {y}";;
        } 
    }

}
// //char(lex.get(lex.oth('x', lex.get('y'))), lex.get(lex.get(lex.get(lex.oth('x',lex.get('y')))))
//     public class Lex
//     {

//         static string Num = 1;

//         public static void Char(char i, char d)
//         {
//             Console.WriteLine(i);
//         }
//         public static void Call(string i, string x, bool value)
//         {
//             Console.WriteLine(x);
//         }

//         static char Oth(char x, char y)
//         {
//             Console.WriteLine($"Oth invoked with {x} and {y}");
//             return y;
//         }
//         static Char Get(char i) => 'c';

//         static char sex(char i, char x) => 'd';
//     }
