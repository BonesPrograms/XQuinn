using XQuinn.Runtime;
namespace XQuinn.Runtime;


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
public class InvokerInterface
{
    protected Type? LastLoadedType;
    protected object? LastInstance;
    public readonly CallInterpreter Interpreter = new();
    public readonly DynamicInvoker? Invoker;

    public InvokerInterface(string? path = null)
    {
        if (path != null)
            Invoker = DynamicInvoker.New(path);
    }

    //DynamicInvoker will have its own op call, something like ?
    //Path will be loaded at compiletime

    public virtual object? Interface(string interf)
    {
        // no throw?
        ArgumentNullException.ThrowIfNullOrWhiteSpace(interf);
        if (interf[0] == '?')
        {
            if (Invoker == null)
                throw new ArgumentException("Cannot access dynamic invoker, it has not been initialized.");
            Invoker.Interface(interf);
        }
        else if (interf[0] == '/')
        {
            return RuntimeCommand.InvokeCommand(interf.Substring(1));
        }
        else //might implement a special char code for this too
            return InterpreterInterface(interf);
        //ret null?
        throw new ArgumentException("No command detected.");
    }

    protected virtual object? InterpreterInterface(string interf)
    {
        object? obj = Interpreter.Interface(interf);
        if (Interpreter.LoadedType != LastLoadedType)
            LastLoadedType = Interpreter.LoadedType;
        if (Interpreter.Instance != LastInstance)
            LastInstance = Interpreter.Instance;
        return obj;
    }

    // virtual for inheritors incase you want to add quickload options
    //for example in qud if you input "player" id return a player object and load it as the interpreter
    //instance directly

}

