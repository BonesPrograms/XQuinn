using System.Reflection;
using System.Text;
using XQuinn.NetConsole;
using XQuinn.Parsing;
using XQuinn.Parsing.AST;
using XQuinn.Reflection;
using XQuinn.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using _xquinn_prgrm;

namespace XQuinn.NetConsole
{

    internal abstract class TestingType
    {

        protected readonly StringBuilder sb = new();
        protected abstract Type TestOf { get; }
        static readonly Dictionary<Type, TestingType> Tests = GetTests();
        protected TestingType()
        {

        }
        protected abstract void Test(string obj);
        public static void Test<T>()
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
                test.WhileTrueTestLoop();
            }
            else
                throw new ArgumentException($"There is no test for type {typeof(T)}.");
        }

        void WhileTrueTestLoop()
        {
            while (true)
            {

                string? text = Console.ReadLine();
                if (text == "exit")
                    return;
                if (text != null)
                    TestAndCatchForDisplay(text);
            }
        }
        void TestAndCatchForDisplay(string obj)
        {
            try
            {
                Test(obj);
            }
            catch (Exception ex)
            {
                sb.Append($"{ex.GetType()}\n");
                sb.Append($"{ex.Message}\n");
                sb.Append($"{ex.StackTrace}\n");
                sb.Append($"{ex.Data}\n{ex.TargetSite}\n{ex.Source}\n");
                Console.WriteLine(sb);
                sb.Length = 0;
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
            bool val = RuntimeCommand.InvokeCommand(obj);
            string invoked = val == true ? "invoked" : "failed";
            Console.WriteLine(invoked);
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

            new InvokerInterface().Interface(obj);


        }

    }
#endif
    internal sealed class CallInterpTest : TestingType
    {
        CallInterpTest()
        {

        }

        static FieldInfo _key = typeof(CallInterpreter).GetField("_key", BindingFlags.NonPublic | BindingFlags.Instance)!;

        static FieldInfo _variableKey = typeof(CallInterpreter).GetField("_variableKey", BindingFlags.NonPublic | BindingFlags.Instance)!;
        protected override Type TestOf => typeof(CallInterpreter);
        readonly CallInterpreter interp = new();
        protected override void Test(string obj)
        {
            Console.Clear();
            Console.WriteLine(DateTime.Now);
            if (obj == "variables")
            {
                ConsoleTools.WriteMany(interp.Variables, x => x.Key);
                return;
            }
            Display(true);
            Console.WriteLine("Invoking...");
            object? ret = interp.Interface(obj);
            Display(false);
        }

        void Display(bool before)
        {
            string timing = before ? "Last Loaded" : "Current Loaded";
            Console.WriteLine($"{timing} Type: {interp.LoadedType}");
            Console.WriteLine($"{timing} Method: {interp.LoadedMethod}");
            Console.WriteLine($"{timing} Instance Type: {interp.InstanceType}");
            string? key = (string?)_key.GetValue(interp);
            Console.WriteLine($"{timing} _key {key}");
            string? variableKey = (string?)_variableKey.GetValue(interp);
            Console.WriteLine($"{timing} _variableKey {variableKey}");

        }
    }
}