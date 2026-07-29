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

namespace _xquinn_cor
{
    file static class xquinn_prgrm_Main
    {
        static void Main(string[] args)
        {
            if (IApp.IApps(args))
                return;
            Core.InitializeXQuinn(false, Core.DefaultToString); // wonder if xq.x.fullname would screw up the lexer.. >:)
            ConsoleTest.Test<CallInterpTest>();
        }

    }

}

namespace Lexing.Gapers
{

    public class Lex
    {

        static int Num = 1;

        public static void Char(char i)
        {
            Console.WriteLine(i);
        }
        public static void Call(string i, int x, bool value)
        {
            Console.WriteLine(x);
        }

        static Char Get() => 'c';
    }
}