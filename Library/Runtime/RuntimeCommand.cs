using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HarmonyLib;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Parsing.AST;



namespace XQuinn.Runtime;

//PRO DEBUGGIGN TIPS:

//If you're trying to invoke a command and it's not being found, double check your method to make sure it can be found and is supported:
//1) Type must have the attribute [RuntimeInvoker] to find the method
//2) Method must be supported:

//UNSUPPORTED: RuntimeCommands do not support methods declared in Generic type definitions. RuntimeCommands do not support instance methods.
//Commands declared under either of these two circumstances will *not* be added to the global cache, the failure will be silent
//short of you not being able to find their name in the command registry.

//RuntimeCommands is backed by CallInterpreter and has all the same features, except it has been restricted to loading methods specifically from the RuntimeCommand cache, instance
//loading and type loading is inaccessible.
//Methods as parameters still work, generic methods still work, fields as parameters still work, and so on.

/// <summary>
/// Tthrown when two or more commands have the same name.
/// </summary>
public class CommandNameDuplicateException : Exception
{
    public CommandNameDuplicateException(string msg) : base(msg)
    {

    }
}


/// <summary>
/// Marks a class as containing a Command or multiple Command methods. Required.
/// </summary>

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RuntimeInvoker : Attribute
{

}

/// <summary>
/// Tags a method as a Command that can be invoked at runtime. Only for static methods.
/// </summary>

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RuntimeCommand : Attribute
{

    /// <summary>
    /// A collection of Commands that can be invoked at runtime.
    /// </summary>
    static readonly ConcurrentDictionary<string, MethodInfo> Registry = new(StringComparer.OrdinalIgnoreCase);
    public static ICollection<string> Keys => Registry.Keys;
    public static ICollection<MethodInfo> Values => Registry.Values;
    static readonly CallInterpreter Interpreter = new();
    public static bool ContainsKey(string key) => Registry.ContainsKey(key);

    public static bool TryGetValue(string key, out MethodInfo? method) => Registry.TryGetValue(key, out method);

    public static IEnumerable<KeyValuePair<string, MethodInfo>> Enumerate()
    {
        foreach (var obj in Registry)
            yield return obj;
    }

    /// <summary>
    /// Invokes Commands by name. Not case sensitive. Returns true if the command was found. Will throw if there is an issue parsing parameters or invoking.
    /// </summary>
    public static bool InvokeCommand(string invoc)
    {
        string name;
        int num = invoc.IndexOf('(');
        if (num != -1)
            name = invoc.Remove(num);
        else
            name = invoc;
        Method method = new(name); //automatically slices off any generic parameters
        if (Registry.TryGetValue(method.String, out MethodInfo? cmd))
        {
            if (cmd.IsGenericMethodDefinition)
            {
                Type[] generics = Interpreter.GetGenerics(method) ?? throw new ArgumentException($"Command {method.String} is generic definition, but no generic parameters were provided.");
                cmd = cmd.MakeGenericMethod(generics);
            }
            object?[]? parameters = null;
            if (num != -1)
            {
                Interpreter.LoadMethodDirect(cmd);
                Method call = Interpreter.Lexer.ParameterTemplate(invoc);
                parameters = Interpreter.GetParams(cmd.GetParameters(), call);
                Interpreter.Clear();
            }
            cmd.Invoke(null, parameters);
            //LogAll($"cmdCall {cmd.Name}::{method.Name}() invoked as Command!");
            return true;
        }
        return false;
    }
    /// <summary>
    /// Must be unique per Command (caseless)
    /// </summary>
    public readonly string Name;
    public RuntimeCommand(string name)
    {
        Name = name;
    }
    //   public static void Register(Module m) => Register(m.GetTypes(), m.Assembly.GetName().FullName);
    public static void Register(IEnumerable<Type> yourtypes, string assembly)
    {
        IEnumerable<Type> types = yourtypes.Where(x => x.GetCustomAttribute<RuntimeInvoker>() != null && !x.IsGenericTypeDefinition);
        Dictionary<string, MethodInfo> commands;
        try
        {
            commands = types
            .SelectMany(x => x.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(CallInterpreter.SupportedMethod)
            .Where(x => x.GetCustomAttribute<RuntimeCommand>() != null).ToDictionary(k => k.GetCustomAttribute<RuntimeCommand>()!.Name, v => v, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException ex)
        {
            string keyname = ex.Message.Substring(ex.Message.IndexOf(':') + 2);
            throw new CommandNameDuplicateException($"Warning: Duplicate Command Names found in local module load for assembly: {assembly}. Key: {keyname}. Please rename one of the conflicting commands. Command names are not case sensitive.");
        }
        foreach (var obj in commands)
        {
            if (Registry.ContainsKey(obj.Key))
                throw new CommandNameDuplicateException($"Warning: Duplicate Command Names found when attempting to add commands to global registry from assembly: {assembly}. Key: {obj}. Please rename one of the conflicting commands. Command names are not case sensitive.");
            Registry.TryAdd(obj.Key, obj.Value);
        }
    }

    // /// <summary>
    // /// Checks if there are any Command attributes with duplicate string names (not case sensitive). Also loads up the Commands cache. Call this after all assemblies are loaded into the appdomain.
    // /// </summary>
    // /// <exception cref="Exception"></exception>
    // public static void Initialize() //this could check the CommandsInfo Array, because it is just doing a name comparison, and the commandinfo array has their string names
    // {
    //     if (Commands != null)
    //         return;
    //     //we can use isdefined for more efficiency afaik
    //     IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
    //     .SelectMany(x => x.GetTypes())
    //     .Where(x => x.GetCustomAttribute<HasCommand>() != null);
    //     Dictionary<string, MethodInfo> commands;
    //     try
    //     {
    //         commands = types
    //         .SelectMany(x => x.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
    //         .Where(CallInterpreter.ValidMethod)
    //         .Where(x => x.GetCustomAttribute<RuntimeCommand>() != null).ToDictionary(k => k.GetCustomAttribute<RuntimeCommand>()!.Name, v => v, StringComparer.OrdinalIgnoreCase);
    //     }
    //     catch (ArgumentException ex)
    //     {
    //         string keyname = ex.Message.Substring(ex.Message.IndexOf(':') + 2);
    //         throw new CommandNameDuplicateException($"Warning: Duplicate Command Names found. Key: {keyname}. Please rename one of the conflicting commands. Command names are not case sensitive.");
    //     }
    //     Commands = new(commands, StringComparer.OrdinalIgnoreCase);
    // }    
}

