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

        internal static Type? _capture;
        static string field = null;

        static void Main(string[] args)
        {
#if IAPP_BUILD
            XQuinn.NetConsole.Apps.IApp.RunApp(args);
#else
            // KeyValuePair<KeyValuePair<int,KeyValuePair<bool,char>>,char>
            Cache();
            object? arr = Array<string>(field);
            Console.WriteLine(arr ?? "null");
          //  method(8);
            //Example();
            RunNavigator();



        }


        static void mutate(ref int i)
        {
            i = 12;
        }

        static class BitConv
        {

        }

        static T[]? Array<T>(params T[]? arr) => arr;
        static T? method<T>() where T : class => null;
        static void RunNavigator()
        {
            NavigationFeed monitor = new();
            string flag = NavigatorCore.Flag.ToString().Replace(',', '|').Replace(" ", "");
            string path = Path.Combine(XQuinn.IO.Finders.CodeLabFinder.s_path, @"XQuinnLib\dump\instance.log");
            string ret = monitor.SafeInterface($"~ *types.enum<bindingFlags>({flag}); +flag; *InstanceReader.new(@\"{path}\", true, types.of<object>()); +reader; *program._capture; +capture", out _, out bool chainexception);
            Console.WriteLine(ret);
             if (chainexception)
               throw new ArgumentException();
            while (true)
            {
                string? msg = Console.ReadLine();
                if (msg != null)
                    Console.WriteLine(monitor.SafeInterface(msg, out _, out _));
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


        class Class
        {
            public string Field;

            public Class(string f)
            {
                Field = f;
            }
        }


    }

    class PrivBase
    {
        int field;
        int method(int i) => i;

        class Nest
        {

        }
    }

    class inh : PrivBase
    {
        int call(int i) => i;

        public int msg(int i) => i;
    }

    class Class<T1, T2> : Class<T1>
    {

    }

    struct Struct
    {
        public int val;

        public int other;

        public Struct(int val, int other)
        {
            this.val = val;
            this.other = other;
        }

        public override string ToString()
        {
            return $"{val} {other}";
        }
    }
    class Class<T> //where T : new()
    {
        static T? S_Obj
        {
            get => _s_obj;
            set => _s_obj = value;
        }
        static T? _s_obj = default;
        T? Obj { get => _obj; set => _obj = value; }
        T? _obj = default;

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


    // static class BytesLittleEndian
    // {

    //     //Not available in net6, not supported.

    //     // public static byte[] AsBytes(UInt128 uint128) => uint128.AsBytesInternal();
    //     //public static byte[] AsBytes(Int128 sint128) => sint128.AsBytesInternal();
    //     public static byte[] AsBytes(nuint uint32or64) => uint32or64.AsBytesInternal();
    //     public static byte[] AsBytes(nint sint32or64) => sint32or64.AsBytesInternal();
    //     public static byte[] AsBytes(ulong uint64) => uint64.AsBytesInternal();
    //     public static byte[] AsBytes(uint uint32) => uint32.AsBytesInternal();
    //     public static byte[] AsBytes(double float64) => float64.AsBytesInternal();
    //     public static byte[] AsBytes(long sint64) => sint64.AsBytesInternal();
    //     public static byte[] AsBytes(float float32) => float32.AsBytesInternal();
    //     public static byte[] AsBytes(int sint32) => sint32.AsBytesInternal();
    //     public static byte[] AsBytes(Half float16) => float16.AsBytesInternal();
    //     public static byte[] AsBytes(ushort uint16) => uint16.AsBytesInternal();
    //     public static byte[] AsBytes(short sint16) => sint16.AsBytesInternal();
    //     public static byte[] AsBytes(char utf16) => utf16.AsBytesInternal();

    //     static byte[] AsBytesInternal<T>(this T num)
    //     {
    //         byte[] bytes = Array.Empty<byte>();

    //         switch (num)
    //         {
    //             // case byte int8:
    //             //     bytes = new byte[1];
    //             //     bytes[0] = int8;
    //             //     break;
    //             case char utf16:
    //                 bytes = new byte[sizeof(char)]; // 2 bytes
    //                 WriteUInt16LittleEndian(bytes, utf16);
    //                 break;
    //             case short sint16:
    //                 bytes = new byte[sizeof(short)]; // 2 bytes
    //                 WriteInt16LittleEndian(bytes, sint16);
    //                 break;
    //             case ushort uint16:
    //                 bytes = new byte[sizeof(ushort)]; // 2 bytes
    //                 WriteUInt16LittleEndian(bytes, uint16);
    //                 break;
    //             case Half float16:
    //                 bytes = new byte[x16bit];
    //                 WriteHalfLittleEndian(bytes, float16);
    //                 break;
    //             case int sint32:
    //                 bytes = new byte[sizeof(int)]; // 4 bytes
    //                 WriteInt32LittleEndian(bytes, sint32);
    //                 break;
    //             case uint uint32:
    //                 bytes = new byte[sizeof(uint)]; // 4 bytes
    //                 WriteUInt32LittleEndian(bytes, uint32);
    //                 break;
    //             case float float32:
    //                 bytes = new byte[sizeof(float)]; // 4 bytes
    //                 WriteSingleLittleEndian(bytes, float32);
    //                 break;
    //             case long sint64:
    //                 bytes = new byte[sizeof(long)]; // 8 bytes
    //                 WriteInt64LittleEndian(bytes, sint64);
    //                 break;
    //             case ulong uint64:
    //                 bytes = new byte[sizeof(ulong)]; // 8 bytes
    //                 WriteUInt64LittleEndian(bytes, uint64);
    //                 break;
    //             case double float64:
    //                 bytes = new byte[sizeof(double)]; // 8 bytes
    //                 WriteDoubleLittleEndian(bytes, float64);
    //                 break;
    //             // case Int128 sint128:
    //             //     bytes = new byte[x128bit]; //absolutely massive
    //             //     WriteInt128LittleEndian(bytes, sint128);
    //             //     break;
    //             // case UInt128 uint128:
    //             //     bytes = new byte[x128bit];
    //             //     WriteUInt128LittleEndian(bytes, uint128);
    //             //     break;
    //             case nint sint32or64:
    //                 if (IntPtr.Size == x32bit)
    //                 {
    //                     bytes = new byte[x32bit];
    //                     WriteInt32LittleEndian(bytes, (int)sint32or64);
    //                 }
    //                 else if (IntPtr.Size == x64bit)
    //                 {
    //                     bytes = new byte[x64bit];
    //                     WriteInt64LittleEndian(bytes, sint32or64);
    //                 }
    //                 else
    //                     throw new PlatformNotSupportedException("Must be 32bit or 64bit process");
    //                 break;
    //             case nuint uint32or64:
    //                 if (!Environment.Is64BitProcess)
    //                 {
    //                     bytes = new byte[x32bit];
    //                     WriteUInt32LittleEndian(bytes, (uint)uint32or64);
    //                 }
    //                 else if (Environment.Is64BitProcess)
    //                 {
    //                     bytes = new byte[x64bit];
    //                     WriteUInt64LittleEndian(bytes, uint32or64);
    //                 }
    //                 else
    //                     throw new PlatformNotSupportedException("Must be 32bit or 64bit process");
    //                 break;
    //                 // case decimal float128:
    //                 // bytes = new byte[x128bit];
    //                 // Write
    //         }
    //         if (bytes.Length == 0)
    //             throw new NotSupportedException("Numeric type not supported by BinaryPrimitives.");
    //         return bytes;
    //     }

    //   }

}


// public class Lex
// {

//     static string Num;

//     public static void Char(char i, char d)
//     {
//         Console.WriteLine(i);
//     }
//     public static void Call(string i, string x, bool value)
//     {
//         Console.WriteLine(x);
//     }

//     static char Oth(char x, char y)
//     {
//         Console.WriteLine($"Oth invoked with {x} and {y}");
//         return y;
//     }
//     static Char Get(char i) => 'c';

//     static char sex(char i, char x) => 'd';
// }

#endif
// //char(lex.get(lex.oth('x', lex.get('y'))), lex.get(lex.get(lex.get(lex.oth('x',lex.get('y')))))
#endif