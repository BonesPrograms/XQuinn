using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Collections.Concurrent;
using HarmonyLib;
using System.Collections.ObjectModel;
using System.Reflection;
using XQuinn.Extensions;
using XQuinn.Parsing.AST;



namespace XQuinn.Runtime
{

    //PRO DEBUGGIGN TIPS:

    //If you're trying to invoke a command and it's not being found, double check your method to make sure it can be found and is supported:
    //1) Type must have the attribute [RuntimeInvoker] to find the method
    //2) Method must be supported:

    //UNSUPPORTED: RuntimeCommands do not support instance methods, or methods with in/out/ref parameters.
    //Generic Type Definitions:
    //If you have a generic method definition who shares the same type parameters as it's enclosing generic type definition, then it will fail to invoke.
    //IE.
    //Class<T>
    //{
    //Method<T>(); //will fail to invoke even if you provide generic arguments
    //}


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
    public sealed class HasRuntimeCommand : Attribute
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
        public static readonly IReadOnlyDictionary<string, MethodInfo> Registry;
        static readonly ConcurrentDictionary<string, MethodInfo> _registry = new(StringComparer.OrdinalIgnoreCase);
        public static ICollection<string> Keys => _registry.Keys;
        public static ICollection<MethodInfo> Values => _registry.Values;
        static readonly CallInterpreter Interpreter = new();
        public static bool ContainsKey(string key) => _registry.ContainsKey(key);

        public static bool TryGetValue(string key, out MethodInfo? method) => _registry.TryGetValue(key, out method);

        public static IEnumerable<KeyValuePair<string, MethodInfo>> Enumerate()
        {
            foreach (var obj in _registry)
                yield return obj;
        }

        static RuntimeCommand()
        {
            Registry = new ReadOnlyDictionary<string, MethodInfo>(_registry);
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
            MethodString method = MethodString.New(name, null, null); //automatically slices off any generic parameters
            if (_registry.TryGetValue(method.String, out MethodInfo? cmd))
            {
                if(!CallInterpreter.SupportedMember(cmd))
                throw new ArgumentException($"RuntimeCommand {name} has an unsupported in out or ref parameter.");
                if (cmd.IsGenericMethodDefinition)
                {
                    cmd = method.ConstructGeneric(cmd);
                }
                object?[]? parameters = null;
                if (num != -1)
                {
                    Interpreter.LoadMethodDirectly(cmd);
                    MethodString call = Interpreter.Lexer.ParameterTemplate(invoc);
                    parameters = Interpreter.GetParsedParameters(cmd.GetParameters(), call);
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
            IEnumerable<Type> types = yourtypes.Where(x => x.GetCustomAttribute<HasRuntimeCommand>() != null);
            Dictionary<string, MethodInfo> commands;
            try
            {
                commands = types
                .SelectMany(x => x.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance)) //we get these just incase you accidentally forget to make it static so it doesn fail silently
                .Where(x => x.GetCustomAttribute<RuntimeCommand>() != null)
                .ToDictionary(k => k.GetCustomAttribute<RuntimeCommand>()!.Name, v => v, StringComparer.OrdinalIgnoreCase);
            }
            catch (ArgumentException ex)
            {
                string keyname = ex.Message.Substring(ex.Message.IndexOf(':') + 2);
                throw new CommandNameDuplicateException($"Warning: Duplicate Command Names found in local module load for assembly: {assembly}. Key: {keyname}. Please rename one of the conflicting commands. Command names are not case sensitive.");
            }
            foreach (var obj in commands)
            {
                if (_registry.TryGetValue(obj.Key, out MethodInfo? cmd))
                {
                    if (cmd != obj.Value)
                        throw new CommandNameDuplicateException($"Warning: Failed to register method {obj.Value.DeclaringType}.{obj.Value} with key: {obj} key is already being used by method {cmd.DeclaringType}.{cmd} Please rename one of the conflicting commands. Command names are not case sensitive.");
                    else
                        continue;
                }
                _registry.TryAdd(obj.Key, obj.Value);
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

}