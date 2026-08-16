using XQuinn.Runtime;
using XQuinn.CodeAnalysis;
using System;
#if NET6_0_OR_GREATER
namespace XQuinn.Runtime
{


    //or my purposes in vwar runtime commands will be invoked starting with a / so you know its not a callinterp. And i guess dynamicinvoked will start with something like !. 
    // Ill have a smartwrapper class that checks these things first and holds instances of callinterp and dynamicinvoker respectivsly. And it also calls runtimecommand ofc. 
    // I can make that part of xquinn. Nonstatic because it will take a path param for dynamicinvoker
    //  (or null then it wont instance the invoker at all)
    //We call it Reflector msybe idk. No we call this one RuntimeInvoker yes. And it wraps all the other kinds of invokers 



    //i want to add a way for this to tell you whats going on and track updates
    //like if you load a type to call interp, it tells you
    //if you invoke a runtime command, it tells you
    //if you load an instance to call interp or invoke from call interp, it tells you, etc


    //questions about thread safety
    //also, if this was instance based, then it could be "individual" and work with multiple people using it in a runtime (maybe)
    //it would at the very least be one step closer to allowing multiple people to use it at the same time
    //runtimecommand would probably take a CallInterpreter parameter in that case rather than having it as a static field

    //currently tho this aint like a multiplayer invoker it rly is intended for individuals to use it clientside generally speaking everything abt their runtime should be exclusive to them
    public sealed class InvokerInterface
    {
        public CallInterpreter Interp => Monitor.Interp;
        public readonly InterpMonitor Monitor = new();
        public readonly DynamicInvoker? Invoker;
        public InvokerInterface(string? assemblyPath = null)
        {
            if (assemblyPath != null)
                Invoker = DynamicInvoker.New(assemblyPath);
        }

        //DynamicInvoker will have its own op call, something like ?
        //Path will be loaded at compiletime

        public object? Interface(string interf, out string? callInterpListenerOutput, bool throwIfNoCmd, out bool exception)
        {
            callInterpListenerOutput = null;
            exception = false;
            if (interf[0] == '~')
            {
                if (Invoker == null)
                    throw new InvalidOperationException("Cannot access dynamic invoker, it has not been initialized.");
                return Invoker.Interface(interf.Substring(1));
            }
            else if (interf[0] == '/')
                return RuntimeCommand.InvokeCommand(interf.Substring(1));
            else if (interf[0] == '#')
            {
                callInterpListenerOutput = Monitor.TryCatchInterface(interf.Substring(1), out object? ret, out exception);
                return ret;
            }
            else
                return throwIfNoCmd ? throw new ArgumentException("No command detected.") : null;
        }

    }

}
#endif