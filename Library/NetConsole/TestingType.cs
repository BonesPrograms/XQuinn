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

namespace XQuinn.NetConsole
{

    internal abstract class TestingType
    {

        protected readonly StringBuilder sb = new();
        protected abstract Type TestOf { get; }
        static readonly Dictionary<Type, TestingType> Tests = GetTests();
        protected virtual void Display(StringBuilder sb) => Console.WriteLine(sb.ToString());
        protected abstract void Test(string obj);

        protected TestingType()
        {
            
        }
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
                {
                    StringBuilder? sb = TryCatchToString(text);
                    if (sb != null)
                        Display(sb);
                }
            }
        }
        StringBuilder? TryCatchToString(string obj)
        {
            try
            {
                Test(obj);
            }
            catch (Exception ex)
            {
                sb.Length = 0;
                sb.Append(ex.GetType());
                sb.Append(' ');
                sb.Append(ex.Message);
                sb.Append(' ');
                sb.Append(ex.StackTrace);
                sb.Append(' ');
             //   sb.Append(ex.Data);
             //   sb.Append(' ');
             //   sb.Append(ex.TargetSite);
              //  sb.Append(' ');
              //  sb.Append(ex.Source);
              //  sb.Append(' ');
                return sb;
            }
            return null;
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

    internal class LexTest : TestingType
    {

        LexTest()
        {
            
        }
        protected override Type TestOf => typeof(InvocationLexer);
        readonly InvocationLexer lex = new();
        protected override void Test(string obj)
        {
            MethodString method = lex.ParameterTemplate(obj);
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
    internal class RuntimeCommandTest : TestingType
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
    internal class RuntimeInvokerTest : TestingType
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
    internal class CallInterpTest : TestingType
    {
        CallInterpTest()
        {
            
        }
        protected override Type TestOf => typeof(CallInterpreter);
        readonly CallInterpreter interp = new();
        protected override void Test(string obj)
        {
            Console.WriteLine("::");
            Console.WriteLine($"Loaded Type: {interp.LoadedType}");
            Console.WriteLine($"Loaded method {interp.LoadedMethod}");
            object? ret = interp.Interface(obj);
            Console.WriteLine("Invoked:");
            Console.WriteLine($"Returned: {ret}");
            Console.WriteLine($"Loaded type: {interp.LoadedType}");
            Console.WriteLine($"Loaded method {interp.LoadedMethod}");
        }
    }
}