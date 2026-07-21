using System.Reflection;
using XQuinn.Reflection;
using XQuinn.Extensions;
using System.Text;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using XQuinn.Parsing;
using System.Runtime.Loader;


namespace XQuinn.Runtime;


/// <summary>
/// Invokes methods from a specific type in a dynamically loaded assembly. Only supports static methods with primitive, enum or string parameters. No in, outs or refs.
/// Gets declared-only members, and does not support overloaded methods.
/// </summary>
/// <summary>
/// Only supports single-file assemblies. Loads an assembly into the runtime and dynamically reloads it if it detects any updates to the file.
/// </summary>
public class DynamicInvoker : IDisposable
{

    class DLLBox : AssemblyLoadContext
    {
        public DLLBox() : base(isCollectible: true)
        {

        }
    }
    DLLBox? Box;
    Assembly? LoadedAssembly;
    public readonly CallInterpreter Interpreter = new();
    /// <summary>
    /// Inheritors should not expose internal reflection data under any circumstances.
    /// </summary>
    protected TypeBook? Book;

    /// <summary>
    /// If you change this, you most certainly will want to call reload afterwards.
    /// </summary>
    public string DLLPath;
    DateTime LastWrite;
    public bool FullName;
    public string? DefaultLoadedTypeName;
    DynamicInvoker(string path) //DynamicReloader exists purely as a base class for DynamicInvoker. It is not intended for anyone else to inherit from.
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(path);
        if (Path.GetExtension(path) != ".dll")
            throw new ArgumentException("only .dll is supported by DynamicReloader");
        DLLPath = path;
    }

    public static DynamicInvoker New(string dllpath)
    {
        DynamicInvoker invoker = new(dllpath);
        invoker.Reload();
        return invoker;
    }
    ~DynamicInvoker()
    {
        Unload();
    }

    public void Reload()
    {
        var monitor = Unload();
        // if (monitor != null)
        //     Collect(monitor);
        Load();
        Module[] modules = LoadedAssembly!.GetModules();
        if (modules.Length > 1)
            throw new NotSupportedException("Only single file assemblies are supported.");
        Interpreter.Book = TypeBook.New(LoadedAssembly.ManifestModule.GetTypes(), FullName);
        if (DefaultLoadedTypeName != null)
            Interpreter.LoadTypeDirect(DefaultLoadedTypeName);
        LastWrite = File.GetLastWriteTime(DLLPath);
    }

    public void Dispose()
    {
        Unload();
        GC.SuppressFinalize(this);
    }



    [MethodImpl(MethodImplOptions.NoInlining)]
    void Load()
    {
        Box = new DLLBox();
        using FileStream stream = File.OpenRead(DLLPath);
        LoadedAssembly = Box.LoadFromStream(stream);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    WeakReference? Unload()
    {
        if (Box == null)
            return null;
        Interpreter.Clear();
        Book = null;
        LoadedAssembly = null;
        WeakReference monitor = new(Box, trackResurrection: true);
        Box?.Unload();
        Box = null;
        return monitor;
    }

    public void Interface(string invocation)
    {
        if (CheckForReload())
            Reload();
        Interpreter.Interface(invocation, true);
    }

    //This is the dynamic part
    //Whenever you try to get or invoke a method it will check if the assembly has been recompiled
    //and will reload its manifest module if so
    bool CheckForReload()
    {
        DateTime write = File.GetLastWriteTime(DLLPath);
        if (write != LastWrite)
        {
            Reload();
            return true;
        }
        return false;
    }

}

//Usage warnings:
//If you are invoking one of your reflected methods via dynamic invoker, your reflected methods should avoid the following behaviors:

//Do not create references to your own user defined classes in objects who's types are from outside of your assembly
//Do not subscribe to any events outside of your assembly
//Do not start background threads or tasks

//If your type provides a static parameterless method named Shutdown, it will be called on reload/unload, which you can use to easily clean up any objects
//you've made outside of your assembly before it is reloaded.
// // The plugin must use this to stop threads, unsubscribe from events, and remove references to objects from their assembly

//You are safe to create, and leave behind, new objects who's types are *not* from your assembly - your goal should be to not leave a trace that your assembly was in the runtime at all.

//If you mess up and there is a trace left over, you will likely experience a memory leak. Currently I have no way of detecting these myself.

//Final Warning: If you create multiple instances of DynamicInvoker that point to the same assembly, you will bloat your memory like crazy
//You should only have one dynamic invoker per assembly loaded
