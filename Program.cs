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
using XQuinn.LangInterp;
using System.Linq;
using System.Collections.Generic;
using XQuinn.Extensions;
using XQuinn.Parsing;
using System.IO;
using XQuinn.LangInterp.SyntaxTree;
using System.Collections;
using XQuinn.ObjectModel;
using System.Runtime.InteropServices;
using System.ComponentModel;
using static XQuinn.Reflection.ByteSizes;
using XQuinn.Runtime.NavigatorEngine;
using XQuinn.NetConsole;

#if IAPP_BUILD
namespace XQuinn.Private
{
    static void Main()
    {
        XQuinn.NetConsole.Apps.IApp.RunApp();
    }
}
#else

enum MyEnum { A, B, C };

static class EnumExt
{
    public static string Something<T>(T obj) where T : struct, Enum
    {
        if (obj.Equals(default(T)))
            return "Hello world";
        return "No!";
    }
}

namespace XQuinn.Private
{


    static class Program
    {

        static Dictionary<GenericKey, Type> Registry { get; } = GetReg();
        static Dictionary<GenericKey, Type>? RegCopy { get; set; }
        internal static Type? InitType { get; set; }
        static void Main()
        {
            Cache();
            RunNavigator();
        }
        static void RunNavigator()
        {
            Navigator.Logger = new ConsoleLogger();
            string[] instructions = new string[]
            {
                $"+ flag = Runtime.Navigator.Flag", "# flag", "+ flags"
            };
            Navigator.Instruct(instructions);
            while (true)
            {
                string? msg = Console.ReadLine();
                if (msg != null)
                    Navigator.Instruct(msg);
            }
        }

        static void Cache()
        {
            Assembly xquinn = Assembly.Load("XQuinn");
            List<Type> types = new(xquinn.GetTypes());
            for (int i = 0; i < types.Count; i++)
            {
                Type t = types[i];
                if (t.Name == "__StaticArrayInitTypeSize=12")
                {
                    InitType = t;
                    types.Remove(t);
                    break;
                }
            }
            TypeRegister.CacheTypes(types, false);
            TypeRegister.CacheType<Harmony>(false);
            TypeRegister.CacheType(typeof(AccessTools), false);
            TypeRegister.CacheType<StringBuilder>(false);
            TypeRegister.CacheType(typeof(StringBuilderExtensions), "sbe");
            TypeRegister.CacheType<ValueType>(false);
            TypeRegister.CacheType(typeof(Path), false);
            TypeRegister.CacheType<WeakReference>(false);
            TypeRegister.CacheType(typeof(WeakReference<>), false);
        }

        static void NamespaceRevert()
        {
            if (RegCopy == null)
                throw new InvalidOperationException();
            Registry.Clear();
            RegCopy.ForEach(x => Registry[x.Key] = x.Value);
            RegCopy = null;
        }

        static void NamespaceTest()
        {
            RegCopy = new(Registry);
            foreach (var obj in RegCopy)
            {
                GenericKey key = obj.Key;
                Type value = obj.Value;
                GenericKey newkey = new($"xq.{key.Name}", key.Args);
                Registry[newkey] = value;
                Registry.Remove(key);
            }
        }

        static Dictionary<GenericKey, Type> GetReg()
        {
            Type reg = typeof(TypeRegister);
            PropertyInfo prop = reg.GetProperty("Registry", BindingFlags.Static | BindingFlags.NonPublic)!;
            object value = prop.GetValue(null, BindingFlags.Static | BindingFlags.NonPublic, null, null, null)!;
            return (Dictionary<GenericKey, Type>)value;
        }

        static void method()
        {
            int[] array = new int[] { 8, 16, 32 };
        }


    }

    class ConsoleLogger : NavigatorLogger
    {

        public override void ToLog(string text)
        {
            Console.WriteLine(text);
        }
        public override void ToChat(string text)
        {

        }
    }

    class The
    {
        static GameObject Player = new GameObject();
    }

    class GameObject
    {
    }
    class Top<T>
    {
        class Nest<X>
        {

        }
    }
    class Objer
    {
        public static object? objer = "Helldasd";
    }

    class Class : Class<object>
    {
        static object? s_obj;
        static object? ret(object? obj) => obj;
    }

    // ~ * Types.Of("Class<T>"); *MakeGenericType(Types.Array<Type>(Types.Of<byte>())); *GetMethod(\"Func\"); Invoke:1(Class<byte>.new(), Types.Array<object>( Types.Num<byte>(37), Types.Num<byte>(80)))
    class Class<T>
    {

        class Nest
        {
            static T obj;
        }

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

        public static ref T RefRet(ref T obj)
        {
            if (obj is int)
            {
                obj = (T)(object)45;
            }
            return ref obj;
        }

        public static T Func2(T obj) => obj;

        public static string Void()
        {
            return "Void";
        }

        public (T, T, T) Func2(T a, T b, T c) => (a, b, c);
        public (T, T) Func(T obj, T obj2) => (obj, obj2);
        public static T Method(T obj) => obj;
        public static X Generic<X>(X obj) => obj;
        public static (X, Z) GenFunc<X, Z>(X obj, Z obj2) => (obj, obj2);
        public static T[] Array(params T[] arr)
        {
            return arr;
        }

        public void Func(int x, int y = 2)
        {

        }
    }



}

#endif
#endif