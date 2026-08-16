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
using _xquinn_prgrm;

namespace XQuinn.NetConsole
{
    internal enum TestOptions
    {
        _invalid = 0,
        Preload
    }
    internal abstract class TestingType
    {

        protected readonly StringBuilder sb = new();
        protected abstract Type TestOf { get; }
        static readonly Dictionary<Type, TestingType> Tests = GetTests();

        protected TestOptions? Option;
        protected TestingType()
        {

        }

        public static void Test<T>(TestOptions? option = null)
        {
            try
            {
                _ = Console.WindowHeight;
            }
            catch (IOException)
            {
                throw new IOException("Cannot run testing types outside of console apps.");
            }
            if (Tests.TryGetValue(typeof(T), out TestingType? test))
            {
                test.Option = option;
                test.WhileTrueTestLoop();
            }
            else
                throw new ArgumentException($"There is no test for type {typeof(T)}.");
        }
        protected virtual string? PreDisplay()
        {
            return null;
        }
        protected abstract void Test(string obj);

        protected virtual void TestAndCatchForDisplay(string obj)
        {
            try
            {
                Test(obj);
            }
            catch (Exception ex)
            {
                sb.CatchException(ex);
                Console.WriteLine(sb);
                sb.Length = 0;
            }
        }
        void WhileTrueTestLoop()
        {
            while (true)
            {
                Console.WriteLine(PreDisplay());
                string? text = Console.ReadLine();
                if (text == "exit")
                    return;
                if (text != null)
                    TestAndCatchForDisplay(text);
            }
        }
        static Dictionary<Type, TestingType> GetTests()
        {                                                                                                       //cant use IsAssignableTo in NetStandard2.0, not a big deal, i dont feel like adding NET6_OR_GREATER precompiler directives whenever I want to run a console test
            IEnumerable<Type> testTypes = typeof(TestingType).Module.GetTypes().Where(x => !x.IsAbstract && typeof(TestingType).IsAssignableFrom(x));
            Dictionary<Type, TestingType> dic = new();
            foreach (var testType in testTypes)
            {
                TestingType test = (TestingType)Activator.CreateInstance(testType, true)!;
                if (dic.TryGetValue(test.TestOf, out TestingType? value))
                    throw new ArgumentException($"There is already a testing type for type {test.TestOf.Name}. Already Existing Testing Type: {value.GetType().Name}. Attempted Insert Testing Type: {testType.Name}");
                dic[test.TestOf] = test;
            }
            return dic;
        }
    }

    internal sealed class LexTest : TestingType
    {

        LexTest()
        {

        }
        protected override Type TestOf => typeof(InvocationLexer);
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
            {
                if (param is MethodString mth)
                    Read(mth);
                else
                    Console.WriteLine($"{param.ToString()} {param.GetType()}");


            }
        }
    }
    internal sealed class RuntimeCommandTest : TestingType
    {
        RuntimeCommandTest()
        {

        }
        protected override Type TestOf => typeof(RuntimeCommand);
        protected override void Test(string obj)
        {
            RuntimeCommand.InvokeCommand(obj);
        }
    }
#if NET6_0_OR_GREATER
    internal sealed class RuntimeInvokerTest : TestingType
    {
        RuntimeInvokerTest()
        {

        }
        protected override Type TestOf => typeof(InvokerInterface);
        protected override void Test(string obj)
        {

            new InvokerInterface().Interface(obj, out _, false, out _);


        }

    }
#endif
    internal sealed class CallInterpTest : TestingType
    {

        protected override Type TestOf => typeof(CallInterpreter);
        CallInterpreter Interp => monitor.Interp;
        readonly InterpMonitor monitor = new();
        InterpPreload? Preload;
        protected override void Test(string obj)
        {
        }
        CallInterpTest()
        {
            Preload = new(monitor.Interp, new string[] {  });
        }
        protected override string? PreDisplay()
        {
            if (Option == TestOptions.Preload)
            {
                if (Preload != null)
                {
                    Preload.Preload();
                    Variable variable = Interp.Variables.FirstOrDefault().Value;
                    Preload = null;
                    return $"{variable} loaded";
                }
            }
            else
                Preload = null;
            Option = null;
            return null;
        }

        protected override void TestAndCatchForDisplay(string obj)
        {            //    Console.Clear();
            string output = monitor.TryCatchInterface(obj, out _, out _);
            Console.WriteLine(output);
        }


    }
}