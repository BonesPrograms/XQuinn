using System.Collections.Concurrent;
using System.Reflection;
using XQuinn.Runtime;
using XQuinn.Reflection;
using XQuinn.NetConsole.Apps;
using XQuinn.CorLib;
using XQuinn;
using XQuinn.NetConsole;
using XQuinn.CorLib.ConsoleTests;

namespace _xquinn_cor;

file static class xquinn_prgrm_Main
{
    static void Main(string[] args)
    {
        if (IApp.IApps(args))
            return;
        Core.InitializeXQuinn(false, Core.DefaultToString);
        //ConsoleTest.Test<CallInterpTest>();
    }
}