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

namespace _xquinn_cor
{


    static class xquinn_prgrm_Main
    {
        static void Main(string[] args)
        {
#if NET6_0_OR_GREATER
    XQuinn.NetConsole.Apps.IApp.RunApp(args);
#endif
            Core.InitializeXQuinn(Core.DefaultToString); // wonder if xq.x.fullname would screw up the lexer.. >:)
            TestingType.Test<CallInterpreter>();


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
