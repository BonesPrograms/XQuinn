using System.Collections.Concurrent;
using System.Reflection;
using XQuinn.Runtime;
using XQuinn.Reflection;
using XQuinn.NetConsole.Apps;
using XQuinn.CorLib;
using XQuinn;
using XQuinn.NetConsole;
using XQuinn.CorLib.ConsoleTests;
using XQuinn.Reflection.IL;
using HarmonyLib;
using XQuinn.IO;
using System.Text;
using Tests;

namespace _xquinn_cor
{
    
    file static class xquinn_prgrm_Main
    {
        static void Main(string[] args)
        {
            if (IApp.IApps(args))
                return;
            Core.InitializeXQuinn(Core.DefaultToString); // wonder if xq.x.fullname would screw up the lexer.. >:)
            ConsoleTest.Test<CallInterpTest>();


            // MethodInfo method = typeof(Static).GetMethod("Print", BindingFlags.Static | BindingFlags.Public)!;
            // object[] arr = new object[]
            // {
            //   "hello"  
            // };
            // method.Invoke(null,arr);

    
        }

    }

}

namespace Tests
{
    public class Static
    {

        public static string Field = "String";
        public static byte ByteField = 255;
        public static void Print(int i) => Console.WriteLine(i);

        public static byte Byte() => 25;
    }
}

// namespace Lexing.Gapers
// {

//     public class Lex
//     {

//         static int Num = 1;

//         public static void Char(char i,char d)
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
// }