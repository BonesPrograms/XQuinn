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
using System.Linq;
using System.Collections.Generic;
using XQuinn.Extensions;

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
             RuntimeCommand.Register(types, "XQuinn");
            Console.Clear();
            TestingType.Test<CallInterpreter>();


        }

    }

    [HasRuntimeCommand]
    class Class
    {

        static int i = 0;
        static readonly string[] _keys = { "one", "two", "three", "four", "five" };
        public static Class @new() => new();
        public string Get() => key;
        public readonly string key;
        public Class()
        {
            key = _keys[i];
            i++;
        }

        public void Print(string i) => Console.WriteLine(i);
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
