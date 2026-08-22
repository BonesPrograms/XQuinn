#if DEBUG_BUILD
using System.Reflection;
using System.Text;
using XQuinn.NetConsole;
using XQuinn.Parsing;
using XQuinn.CodeAnalysis.AST;
using XQuinn.CodeAnalysis;
using XQuinn.Reflection;
using XQuinn.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using XQuinn.Extensions;

namespace XQuinn.NetConsole
{

    internal abstract class TestingType
    {

        protected readonly StringBuilder sb = new();
        protected abstract Type TestOf { get; }
        static readonly Dictionary<Type, TestingType> Tests = GetTests();
        bool preloadComplete;
        protected TestingType()
        {

        }

        public static void Test<T>(params string[] preload)
        {
            try { _ = Console.WindowHeight; }
            catch (IOException) { throw new IOException("Cannot run testing types outside of console apps."); }
            if (Tests.TryGetValue(typeof(T), out TestingType? test)) test.WhileTrueTestLoop(preload);
            else throw new ArgumentException($"There is no test for type {typeof(T)}.");
        }
        protected virtual string? PreLoad(IEnumerable<string>? preload)
        {
            return null;
        }
        protected virtual void Test(string obj)
        {

        }

        protected virtual void TestAndCatchForDisplay(string obj)
        {
            try { Test(obj); }
            catch (Exception ex)
            {
                sb.CatchException(ex);
                Console.WriteLine(sb);
                sb.Length = 0;
            }
        }
        void WhileTrueTestLoop(IEnumerable<string>? preload)
        {
            while (true)
            {
                if (!preloadComplete)
                {
                    string? pre = PreLoad(preload);
                    if (pre != null) Console.WriteLine(pre);
                    preloadComplete = true;
                }
                string? text = Console.ReadLine();
                if (text == "exit") return;
                if (text != null) TestAndCatchForDisplay(text);
            }
        }
        static Dictionary<Type, TestingType> GetTests()
        {                                                                                                       //cant use IsAssignableTo in NetStandard2.0, not a big deal, i dont feel like adding NET6_OR_GREATER precompiler directives whenever I want to run a console test
            IEnumerable<Type> testTypes = typeof(TestingType).Module.GetTypes().Where(x => !x.IsAbstract && typeof(TestingType).IsAssignableFrom(x));
            Dictionary<Type, TestingType> dic = new();
            foreach (var testType in testTypes)
            {
                TestingType test = (TestingType)Activator.CreateInstance(testType, true)!;
                if (dic.TryGetValue(test.TestOf, out TestingType? value)) throw new ArgumentException($"There is already a testing type for type {test.TestOf.Name}. Already Existing Testing Type: {value.GetType().Name}. Attempted Insert Testing Type: {testType.Name}");
                dic[test.TestOf] = test;
            }
            return dic;
        }
    }

    internal abstract class TestingType<T> : TestingType
    {
        protected sealed override Type TestOf => typeof(T);
    }
    internal sealed class LexTest : TestingType<InvocationLexer>
    {

        LexTest()
        {

        }
        readonly InvocationLexer lex = new();
        protected override void Test(string obj)
        {
            MethodString method = lex.ParameterTemplate(obj, "discard");
            Read(method);
        }

        static void Read(MethodString method)
        {
            Console.WriteLine($"{method.ToString()} {method.GetType()}");
            foreach (var param in method.Params)
                if (param is MethodString mth) Read(mth);
                else Console.WriteLine($"{param.ToString()} {param.GetType()}");
        }
    }
    internal sealed class RuntimeCommandTest : TestingType<Command>
    {
        RuntimeCommandTest()
        {

        }

        readonly RuntimeNavigator interp = new();
        protected override void Test(string obj)
        {
            Command.InvokeCommand(obj, interp, out _);
        }
    }

    internal sealed class RuntimeInvokerTest : TestingType<RuntimeGateway>
    {

        RuntimeGateway Gateway = new();
        RuntimeInvokerTest()
        {

        }
        protected override void Test(string obj)
        {
            #if NET6_0_OR_GREATER
            Gateway.Interface(obj, out _, false, out _, out _, out _);
            #else
            Gateway.Interface(obj, out _ , false, out _);
            #endif


        }

    }

    internal sealed class CallInterpTest : TestingType<RuntimeNavigator>
    {

        RuntimeNavigator Interp => monitor.Interp;
        readonly NavigationMonitor monitor = new();

        protected override void TestAndCatchForDisplay(string obj)
        {
            string output = monitor.TryCatchInterface(obj, out _, out _);
            Console.WriteLine(output);
        }


    }
}
#endif