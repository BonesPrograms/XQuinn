#if !RELEASE_BUILD
using System.Collections.Concurrent;
using System.Reflection;
using XQuinn.Runtime;
using XQuinn.Reflection;
using XQuinn;
using HarmonyLib;
using XQuinn.IO;
using System.Text;
using System;
using System.Runtime.Versioning;
using XQuinn.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using XQuinn.Extensions;
using XQuinn.Parsing;
using System.IO;
using XQuinn.CodeAnalysis.AST;
using System.Collections;
using XQuinn.ObjectModel;
using System.Runtime.InteropServices;
using System.ComponentModel;
using static XQuinn.Reflection.ByteSizes;
using XQuinn.Runtime.NavigatorEngine;
using XQuinn.NetConsole;

namespace XQuinn.Private
{


    static class Program
    {
        static void Main(string[] args)
        {
#if IAPP_BUILD
            XQuinn.NetConsole.Apps.IApp.RunApp(args);
#else
            Cache();
            ConsoleTools.WriteMany(TypeRegister.s_registry, "\n");
            RunNavigator();
        }
        //  static void method()
        //  {
        //      int[] array = new int[] { 8, 16, 32 };
        // }

        static Dictionary<GenericKey, Type>? s_reg_copy;

        static void NamespaceRevert()
        {
            if (s_reg_copy == null)
                throw new InvalidOperationException();
            TypeRegister.s_registry.Clear();
            s_reg_copy.ForEach(x => TypeRegister.s_registry[x.Key] = x.Value);
        }

        static void NamespaceTest()
        {
            s_reg_copy = new(TypeRegister.s_registry);
            foreach (var obj in s_reg_copy)
            {
                GenericKey key = obj.Key;
                Type value = obj.Value;
                GenericKey newkey = new($"xq.{key.Name}", key.Args);
                TypeRegister.s_registry[newkey] = value;
                TypeRegister.s_registry.Remove(key);
            }
        }
        static void RunNavigator()
        {
            NavigationFeed monitor = new();
            monitor.StackTrace = true;
            string flag = NavigatorCore.Flag.ToString().Replace(',', '|');
            string path = Path.Combine(XQuinn.IO.Finders.CodeLabFinder.s_path, @"XQuinnLib\dump\instance.log");
            string[] instructions = new string[]
            {$"~ *types . enum < bindingFlags > ( {flag} ); +flag;",
            $"*InstanceReader . new (  @\"{path}\"  , true,  types . of < object > () ); +reader;",
            "*class<float>.new();",
            "Program.NamespaceTest();",
            "* xq.class <xq.object> .new();",
            "*xq . KVP<xq. int,xq .string> .  new(22, \"Hello World\"); +kv;",
            "*xq.class<xq.object>.new();",
            "*xq.types.of(\"XQ.Class<T>\"); +class_T_def;",
            "*class_T_def.MakeGenericType( xq.Types. Array<xq.Type>(xq .Types .Of<XQ. Object>())); + class_typeof_object;",
            "* GetMethod ( \"Func\" ); +method_func;",
            " * xq.class<xq.float>.new(); +float_class;"};
            //"xq.Class< xq . kvp< xq . string , xq . int>>.new()"};
            StringBuilder sb = new();
            instructions.ForEach(x => sb.Append(x));
            string ret = monitor.SafeInterface(sb.ToString());
            Console.WriteLine(ret);
            while (true)
            {
                string? msg = Console.ReadLine();
                if (msg != null)
                    Console.WriteLine(monitor.SafeInterface(msg));
            }
        }
        static void Cache()
        {
            Assembly xquinn = Assembly.Load("XQuinn");
            TypeRegister.CacheTypes(xquinn.GetTypes(), false);
            TypeRegister.CacheType<Harmony>(false);
            TypeRegister.CacheType(typeof(AccessTools), false);
            TypeRegister.CacheType(typeof(AccessToolsExtensions), "accesstoolsE");
        }

    }
    class Class<T>
    {

        static List<T?> list = new() { default, default, default, default };
        static T? S_Obj
        {
            get => _s_obj;
            set => _s_obj = value;
        }
        static T? _s_obj;
        T? Obj { get => _obj; set => _obj = value; }
        T? _obj;

        public static object? Nullobj => null;

        public static T Func2(T obj) => obj;

        public static string Void()
        {
            return "Void";
        }
        public (T, T) Func(T obj, T obj2) => (obj, obj2);
        public static T Method(T obj) => obj;
        public static X Generic<X>(X obj) => obj;
        public static (X, Z) GenFunc<X, Z>(X obj, Z obj2) => (obj, obj2);
        public static T[] Array(params T[] arr)
        {
            return arr;
        }
    }

}

#endif
#endif