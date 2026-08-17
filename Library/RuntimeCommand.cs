using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Collections.Concurrent;
using HarmonyLib;
using System.Collections.ObjectModel;
using System.Reflection;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis;
using XQuinn.CodeAnalysis.AST;
using XQuinn.Reflection;



namespace XQuinn
{

    //PRO DEBUGGIGN TIPS:

    //If you're trying to invoke a command and it's not being found, double check your method to make sure it can be found and is supported:
    //1) Type must have the attribute [RuntimeInvoker] to find the method
    //2) Method must be supported:

    //UNSUPPORTED: RuntimeCommands must consist of alphanumeric and underscore characters only.
    // RuntimeCommands do not support instance methods, or methods with in/out/ref parameters.
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
    public class RuntimeCommandException : Exception
    {
        public RuntimeCommandException(MethodInfo method, RuntimeCommand command, string msg) : base($"Method {method} in type {method.DeclaringType} with command name {command.Name} {msg}")
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

        //        static readonly CallInterpreter Interpreter = new();
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
        public static bool InvokeCommand(string invocation, CallInterpreter interp, out object? ret)
        {
            ret = null;
            string name = invocation;
            int methodStart = invocation.IndexOf('(');
            bool isMethod = methodStart != -1;
            if (isMethod) name = invocation.Remove(methodStart);
            MethodString method = MethodString.New(name, null, null); //automatically slices off any generic parameters
            if (_registry.TryGetValue(method.String, out MethodInfo? cmd))
            {
                if (cmd.IsGenericMethodDefinition) cmd = method.ConvertToGeneric(cmd);
                object?[]? parameters = null;
                if (isMethod)
                {
                    interp.LoadMethodDirectly(cmd);
                    MethodString call = interp.Lexer.ParameterTemplate(invocation, null);
                    parameters = interp.GetParsedParameters(cmd.GetParameters(), call);
                    interp.Clear();
                }
                ret = cmd.Invoke(null, parameters);
                return true;
                //LogAll($"cmdCall {cmd.Name}::{method.Name}() invoked as Command!");
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
        public static void Register(IEnumerable<Type> types)
        {
            foreach (Type type in types)
                if (type.GetCustomAttribute<HasRuntimeCommand>() != null)
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance))
                    {
                        RuntimeCommand? command = method.GetCustomAttribute<RuntimeCommand>();
                        if (command != null)
                        {
                            if (!method.IsStatic) throw new RuntimeCommandException(method, command, "is not static.");
                            TypeCache.ThrowIfBadKey(command.Name);
                            if (!CallInterpreter.SupportedMember(method)) throw new RuntimeCommandException(method, command, "has an unsupported in out or ref parameter.");
                            if (_registry.TryGetValue(command.Name, out MethodInfo? alreadyCached)) { if (method != alreadyCached) throw new RuntimeCommandException(method, command, "cannot be cached, command name is already taken."); else continue; }
                            _registry.TryAdd(command.Name, method);
                        }
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