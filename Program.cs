using System.Collections.Concurrent;
using System.Reflection;
using XQuinn.Runtime;
using XQuinn.Reflection;
using XQuinn.CorLib;
using XQuinn;
using XQuinn.NetConsole;
using HarmonyLib;
using XQuinn.IO;
using System.Text;
using System;
using System.Runtime.Versioning;
using System.Linq;
using System.Collections.Generic;

namespace _xquinn_prgrm
{


    static class _xquinn_prgrm_Main
    {
        static void Main(string[] args)
        {
#if NET6_0_OR_GREATER
    XQuinn.NetConsole.Apps.IApp.RunApp(args);
#endif
            TypeCache.CacheType("class", typeof(Class));
            TypeCache.CacheType("base", typeof(Base));
            TestingType.Test<CallInterpreter>();


        }

    }


    abstract class Base
    {
        public virtual void Method()
        {
            Console.WriteLine("INVOKING BASE");
        }
    }


    class Class : Base
    {
        int Field = 15;
        public static Class @class = new();
        public override void Method()
        {
            Console.WriteLine("INVOKING CLASS");
        }
    }



}
// //char(lex.get(lex.oth('x', lex.get('y'))), lex.get(lex.get(lex.get(lex.oth('x',lex.get('y')))))
//     public class Lex
//     {

//         static int Num = 1;

//         public static void Char(char i, char d)
//         {
//             Console.WriteLine(i);
//         }
//         public static void Call(string i, int x, bool value)
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
