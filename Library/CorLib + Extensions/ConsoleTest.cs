using System.Reflection;
using System.Text;
using XQuinn.NetConsole;
using XQuinn.Reflection;
using XQuinn.Runtime;

namespace XQuinn.CorLib.ConsoleTests;


//Should this be an interface instead?
internal abstract class ConsoleTest
{

    // static readonly Type[] RuntimeTests = typeof(RuntimeTest<>).Module.GetTypes().Where(x => x != typeof(RuntimeTest<>) && x.IsAssignableTo(typeof(RuntimeTest<>))).ToArray();
    //so used to reflection i forgot im using generics
    //this method pretty much is the cache of runtime test instances lol
    public static void Test<X>() where X : ConsoleTest, new()
    {
        // Type type = RuntimeTests.First(x => x == typeof(X));
        //      if (!typeof(X).BaseType!.GetGenericArguments().Contains(typeof(T)))
        //     throw new ArgumentException($"Func return type is {typeof(T)}, which does not match generic parameters of {new ReflectionReader(typeof(X))}");
        new X().DefaultTest();
        // MethodInfo method = type.GetMethod(nameof(DefaultTest), BindingFlags.Public | BindingFlags.Instance)!;
        // object[] arr = [ret];
        // method.Invoke(instance, arr);
    }

    public void DefaultTest()
    {
        while (true)
        {
            string? obj = Console.ReadLine();
            if (obj != null)
            {
                var sb = TryCatchTest(obj);
                if (sb != null)
                    Display(sb);
            }
        }
    }
    StringBuilder? TryCatchTest(string obj)
    {
        try
        {
            Test(obj);
        }
        catch (Exception ex)
        {
            StringBuilder sb = new();
            sb.Append(ex.GetType());
            sb.Append(' ');
            sb.Append(ex.Message);
            sb.Append(' ');
            sb.Append(ex.StackTrace);
            sb.Append(' ');
            sb.Append(ex.Data);
            sb.Append(' ');
            sb.Append(ex.TargetSite);
            sb.Append(' ');
            sb.Append(ex.Source);
            sb.Append(' ');
            return sb;
        }
        return null;
    }

    protected virtual void Display(StringBuilder sb) => Console.WriteLine(sb);
    protected abstract void Test(string obj);
}
internal class RuntimeCommandTest : ConsoleTest
{
    protected override void Test(string obj)
    {
        bool val = RuntimeCommand.InvokeCommand(obj);
        string invoked = val == true ? "invoked" : "failed";
        Console.WriteLine(invoked);
    }
}
internal class RuntimeInvokerTest : ConsoleTest
{
    protected override void Test(string obj)
    {
        new InvokerInterface().Interface(obj);
    }

}

internal class CallInterpTest : ConsoleTest
{

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
