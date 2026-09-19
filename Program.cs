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
            RunNavigator();
        }

        static void RunNavigator()
        {
            NavigationFeed monitor = new();
            string flag = NavigatorCore.Flag.ToString().Replace(',', '|').Replace(" ", "");
            string path = Path.Combine(XQuinn.IO.Finders.CodeLabFinder.s_path, @"XQuinnLib\dump\instance.log");
            string ret = monitor.SafeInterface($"~ *types.enum<bindingFlags>({flag}); +flag; *InstanceReader.new(@\"{path}\", true); +reader; @program");
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
            TypeCache.CacheTypes(xquinn.GetTypes(), false);
            TypeCache.CacheType<Harmony>(false);
            //TypeCache.CacheType(typeof(KeyValuePair<,>), false);
            TypeCache.CacheType(typeof(AccessTools), false);
            TypeCache.CacheType(typeof(AccessToolsExtensions), "accesstoolsE");
            //   TypeCache.CacheType(typeof(BitConverter), false);
            //@ TypeCache.CacheType(typeof(BytesLittleEndian), "bytes");
        }

        class Class { }

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
}

#endif
#endif