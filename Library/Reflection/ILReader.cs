#if NET6_0_OR_GREATER
using System.Reflection.Emit;
using System.Reflection;
using System.Buffers.Binary;
using static System.Buffers.Binary.BinaryPrimitives; //binaryprimitives is preferred to bitconverter cause it is more efficient and little endians for me
using static XQuinn.Reflection.ILReader;
using XQuinn.IO;
using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using System.Text;
using System.Numerics;
using XQuinn.Extensions;
using System.ComponentModel;
using XQuinn.Reflection;
using System.Linq;
using static XQuinn.Reflection.ByteSizes;

namespace XQuinn.Reflection
{

    //Todo: Reading delegates, reading delegate bodies?


    //learn wtf an offset is in IL

    //Update:
    //This has gotten pretty good now, the only thing it cant really read yet are jump labels. Otherwise it is able to read shit pretty much just as good as the MonoCecil ILReader

    /// <summary>
    /// Converts a method into readable IL instructions. 
    /// </summary>
    public sealed class ILReader : IDisposable
    {

        IList<LocalVariableInfo> _localvars = null!;
        ParameterInfo[] _params = null!;
        byte[] _il = null!;
        Module _module = null!;
        MethodBase _methodbase = null!;

        /// <summary>
        /// Some opcodes are 2 bytes long, they will always start with a "prefix" byte.
        /// </summary>
        const byte PrefixBit = 0xFE;
        Type[]? _generic_method_args;
        Type[]? _generic_type_args;

        readonly StreamWriter _writer;
        ILReader(string path)
        {
            _writer = new(path);
        }
        public static void PrintIL(MethodBase method, string outputFilePath, bool makeFileIfNotFound)
        {
            using ILReader reader = New(outputFilePath, makeFileIfNotFound);
            reader.Update(method);
            reader.PrintIL();
        }


        public static ILReader New(string outputFilePath, bool makeFileIfNotFound)
        {
            if (makeFileIfNotFound)
                XQuinn.IO.Logger.SafetyCheck(outputFilePath);
            ILReader reader = new(outputFilePath);
            return reader;
        }

        public void Update(MethodBase method)
        {
            if (Harmony.GetPatchInfo(method) != null)
                throw new NotSupportedException($"{method} in {method.DeclaringType} has been patched by harmony and it's actual behavior cannot properly be represented by ILReader.");
            MethodBody body = method.GetMethodBody() ?? throw new ArgumentException("Method body is null.");
            _methodbase = method;
            _module = method.Module;
            _localvars = body.LocalVariables;
            _il = body.GetILAsByteArray() ?? throw new ArgumentException("Byte array is null.");
            _params = method.GetParameters();
            if (method is MethodInfo)
                _generic_method_args = method.GetGenericArguments();
            _generic_type_args = method.DeclaringType?.GetGenericArguments();
        }


        // public string ReadIL()
        // {
        //     StringBuilder sb = new();
        // }

        /// <summary>
        /// Prints readable IL to a file.
        /// </summary>
        /// <param name="outputFilePath"></param>
        public void PrintIL() //sho
        {
            if (_methodbase == null)
                throw new ArgumentNullException(nameof(_methodbase));
            _writer.WriteLine("method");
            _writer.Write("  ");
            string methodstring;
            if (_methodbase is MethodInfo mthdinfo)
                methodstring = MetadataPrinter.MethodToString(new(), mthdinfo, true).ToString();
            else
                methodstring = MetadataPrinter.ConstructorToString(new(), (ConstructorInfo)_methodbase).ToString();
            _writer.WriteLine($"  {methodstring}");//,maybe should edit the stringbuilder to slip in parameter names
            _writer.WriteLine("");
            //  writer.WriteLine("	.maxstack 1");
            _writer.WriteLine("Locals");
            for (int i = 0; i < _localvars.Count; i++)
            {
                bool needcomma = For.NeedsDelimiter(_localvars.Count, i);
                char? comma = needcomma ? ',' : null;
                _writer.WriteLine($"		[{i}] {_localvars[i].LocalType.Name}{comma}");

            }
            _writer.WriteLine("");
            List<ByteCode> codes = GetIL();
            foreach (ByteCode code in codes)
                _writer.WriteLine("	" + code.ToString());
        }

        public void Dispose()
        {
            _writer.Close();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns a list of readable IL.
        /// </summary>
        /// <returns></returns>
        List<ByteCode> GetIL()
        {
            int i = 0;
            List<ByteCode> codes = new();
            while (i < _il.Length)
                BytesToIL(codes, ref i);
            return codes;
        }

        void BytesToIL(List<ByteCode> codes, ref int i)
        {
            OpCode code = GetOpCode(ref i);
            int size = OperandSize(code.OperandType);
            object token = GetToken(i, ref size, code.OperandType);
            object? operand = size == x0bit ? null : GetOperand(code, token, i);
            ByteCode instruction = new(code, operand, i - 1);
            codes.Add(instruction); //for some reason it is off by 1, you want to shift the offset back by 1 or every label will be off by 1 individually and cumulatively so all labels will be off
            i += size;
        }

        OpCode GetOpCode(ref int i)
        {
            OpCode code;
            byte indexedbyte = _il[i];
            if (indexedbyte == PrefixBit)
            {
                byte nextbyte = _il[i + 1];
                short key = (short)((PrefixBit << 0x08) | nextbyte);
                code = OpCodeMap.s_opCodes[key];
                i += 2;
            }
            else
            {
                code = OpCodeMap.s_opCodes[indexedbyte];
                i++;
            }
            return code;
        }


        object GetToken(int i, ref int size, OperandType type)
        {
            if (size == x0bit)
                return x0bit; //no operand
            else if (size == x8bit)
                return _il[i];
            else if (size == x16bit)
                return BinaryPrimitives.ReadInt16LittleEndian(_il.AsSpan(i, x16bit));
            else if (size == x32bit)
            {
                int token;
                if (type == OperandType.ShortInlineR)
                    return BinaryPrimitives.ReadSingleLittleEndian(_il.AsSpan(i, x32bit));
                else
                    token = BinaryPrimitives.ReadInt32LittleEndian(_il.AsSpan(i, x32bit)); ;
                if (type == OperandType.InlineSwitch)
                    size += token * 4;
                return token;
            }
            else if (size == x64bit)
            {
                if (type == OperandType.InlineR)
                    return BinaryPrimitives.ReadDoubleLittleEndian(_il.AsSpan(i, x64bit));
                return BinaryPrimitives.ReadInt64LittleEndian(_il.AsSpan(i, x64bit));
            }
            throw new InvalidOperationException("OperandSize(OperandType) returned an out-of-range value.");
        }

        object? GetOperand(OpCode code, object token, int i)
        =>
            code.OperandType switch
            {
                OperandType.ShortInlineBrTarget => (sbyte)(byte)token + i + 1,
                OperandType.ShortInlineI => (sbyte)(byte)token,
                OperandType.InlineI => (int)token, //these are kept for sorting later in bytecode; if you get a token that is int and its operand is not a valuetype, you know its a class
                OperandType.ShortInlineR => (float)token, //i could maybe just make all these return null but then id have to do a lot of patern matching to assign the 
                OperandType.InlineR => (double)token, //"token" value to the operand in the ByteCode constructor so fuck that
                OperandType.InlineI8 => (long)token, //maybe i could just check if the token is not int... would require changing the base constructor since it takes the operand first
                OperandType.ShortInlineVar => GetVariable(code, (byte)token), //or something
                OperandType.InlineVar => GetVariable(code, (short)token),
                OperandType.InlineMethod => _module.ResolveMethod((int)token, _generic_type_args, _generic_method_args),
                OperandType.InlineField => _module.ResolveField((int)token, _generic_type_args, _generic_method_args),
                OperandType.InlineType => _module.ResolveType((int)token, _generic_type_args, _generic_method_args),
                OperandType.InlineString => _module.ResolveString((int)token),
                OperandType.InlineTok => _module.ResolveMember((int)token, _generic_type_args, _generic_method_args),
                OperandType.InlineSig => _module.ResolveSignature((int)token),
                OperandType.InlineNone or OperandType.InlineSwitch => null, //the "token" is the jump target, and will carry over for display in ByteCode, so operand is unecessary
                OperandType.InlineBrTarget => (int)token + i + 1, //idk why u need to do +1 but it is always -1 instruction off from its actual jump target
                _ => throw new InvalidProgramException("Operand type does not exist.")
            };

        object GetVariable(OpCode opcode, int token)
        {
            if (opcode.Name?.Contains("loc") ?? false)
            {
                return _localvars[token];
            }
            else
            {
                if (_methodbase.IsStatic)
                {
                    return _params[token];
                }
                else if (token == 0) //idk why a token of 0 == this, but i guess that is the byte code for this, wonder where i can learn that
                    return "this"; //idk how to get this, was trying to research it monocecil, but cant access the class they use to resolve waht this is
                return _params[token - 1]; //not sure why they do -1 here, not sure why i do +1 for the jump label lol
            }                              //i suppose it has something to do with being nonstatic? but why -1? is "this" on the params list?
        }

#pragma warning disable CS8509 // OperandType.InlinePhi is excluded because it is obsolete.
        static byte OperandSize(OperandType type) =>
        type switch
        {
            OperandType.InlineNone => x0bit,
            OperandType.ShortInlineVar or OperandType.ShortInlineI or OperandType.ShortInlineBrTarget => x8bit,
            OperandType.InlineVar => x16bit,
            OperandType.InlineTok or OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineType or OperandType.ShortInlineR or OperandType.InlineSwitch => x32bit,
            OperandType.InlineR or OperandType.InlineI8 => x64bit
        };
#pragma warning restore CS8509
    }

    static class OpCodeMap
    {

        /// <summary>
        /// A dictionary of opcodes that can be indexed by their short value (OpCode.Value).
        /// </summary>
        public static readonly IReadOnlyDictionary<short, OpCode> s_opCodes = Map();

        static Dictionary<short, OpCode> Map()
        {
            Dictionary<short, OpCode> dic = typeof(OpCodes)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(x => x.FieldType == typeof(OpCode))
            .Select(x => (OpCode)x.GetValue(null)!).ToDictionary(x => x.Value, v => v);
            return dic;
        }

    }
    /// <summary>
    /// Readable IL instruction.
    /// </summary>
    internal readonly struct ByteCode
    {

        public static bool ShowOperandType = false;

        readonly int Offset; //this is kind of fucking useless to end users right now, since this isnt really part of some sort of reflection.emit system. emit.label?
        public readonly OpCode OpCode;
        public readonly object? Operand;
       // public readonly int? Token;
        internal ByteCode(OpCode opcode, object? operand, int offset)
        {
            Operand = operand;
            OpCode = opcode;
            Offset = offset;
          //  Token = null;
           // if (operand is not null and not LocalVariableInfo and not ParameterInfo and not ValueType)
           // {
           //     if (token is int integer)
             //       Token = integer;
           // }

        }

        public readonly override string ToString()
        {
            StringBuilder sb = new();
            sb.Append($"IL_{Offset:x4}: ");
            sb.Append(OpCode.ToString());
            if (ShowOperandType)
                sb.Append($" {OpCode.OperandType}");
            if (Operand is ConstructorInfo)
                sb.Append(" instance void");
            sb.Append(' ');
            if (Operand != null)
                OperandToString(sb);
            return sb.ToString();
        }
        readonly void OperandToString(StringBuilder sb)
        {
            if (Operand is LocalVariableInfo lvar)
            {
                MetadataPrinter.GenericTypeToString(sb, lvar.LocalType);
                sb.Append($" {lvar.LocalIndex}");
            }
            else if (Operand is ParameterInfo info)
            {
                MetadataPrinter.GenericTypeToString(sb, info.ParameterType);
                sb.Append($" {info.Name}");
            }
            else if (OpCode.OperandType == OperandType.InlineBrTarget || OpCode.OperandType == OperandType.ShortInlineBrTarget)
            {
                if (Operand == null)
                    throw new InvalidOperationException();
                int num = (int)Operand;
                sb.Append($"IL_{num:x4}");
            }
            else if (Operand is string)
                sb.Append($"\"{Operand}\"");
            else if (Operand is MemberInfo inf)
                MetadataPrinter.BuildPrint(sb, inf!);
            else
                sb.Append($"{Operand?.ToString()}");
        }
    }


    static class ByteSizes
    {
        /// <summary>
        /// 0 bytes.
        /// </summary>
        public const int x0bit = 0x00;

        /// <summary>
        /// 1 byte.
        /// </summary>
        public const int x8bit = 0x01;
        /// <summary>
        /// 2 bytes.
        /// </summary>
        public const int x16bit = 0x02;
        /// <summary>
        /// 4 bytes.
        /// </summary>
        public const int x32bit = 0x04;
        /// <summary>
        /// 8 bytes.
        /// </summary>
        public const int x64bit = 0x08;

        //  public const int x128bit = 0x16;

        //  public const int x256bit = 0x32;
    }


    static class BytesLittleEndian
    {

        //Not available in net6, not supported.

        // public static byte[] AsBytes(UInt128 uint128) => uint128.AsBytesInternal();
        //public static byte[] AsBytes(Int128 sint128) => sint128.AsBytesInternal();
        public static byte[] AsBytes(nuint uint32or64) => uint32or64.AsBytesInternal();
        public static byte[] AsBytes(nint sint32or64) => sint32or64.AsBytesInternal();
        public static byte[] AsBytes(ulong uint64) => uint64.AsBytesInternal();
        public static byte[] AsBytes(uint uint32) => uint32.AsBytesInternal();
        public static byte[] AsBytes(double float64) => float64.AsBytesInternal();
        public static byte[] AsBytes(long sint64) => sint64.AsBytesInternal();
        public static byte[] AsBytes(float float32) => float32.AsBytesInternal();
        public static byte[] AsBytes(int sint32) => sint32.AsBytesInternal();
        public static byte[] AsBytes(Half float16) => float16.AsBytesInternal();
        public static byte[] AsBytes(ushort uint16) => uint16.AsBytesInternal();
        public static byte[] AsBytes(short sint16) => sint16.AsBytesInternal();
        public static byte[] AsBytes(char utf16) => utf16.AsBytesInternal();

        static byte[] AsBytesInternal<T>(this T num) 
        {
            byte[] bytes = Array.Empty<byte>();

            switch (num)
            {
                // case byte int8:
                //     bytes = new byte[1];
                //     bytes[0] = int8;
                //     break;
                case char utf16:
                    bytes = new byte[sizeof(char)]; // 2 bytes
                    WriteUInt16LittleEndian(bytes, utf16);
                    break;
                case short sint16:
                    bytes = new byte[sizeof(short)]; // 2 bytes
                    WriteInt16LittleEndian(bytes, sint16);
                    break;
                case ushort uint16:
                    bytes = new byte[sizeof(ushort)]; // 2 bytes
                    WriteUInt16LittleEndian(bytes, uint16);
                    break;
                case Half float16:
                    bytes = new byte[x16bit];
                    WriteHalfLittleEndian(bytes, float16);
                    break;
                case int sint32:
                    bytes = new byte[sizeof(int)]; // 4 bytes
                    WriteInt32LittleEndian(bytes, sint32);
                    break;
                case uint uint32:
                    bytes = new byte[sizeof(uint)]; // 4 bytes
                    WriteUInt32LittleEndian(bytes, uint32);
                    break;
                case float float32:
                    bytes = new byte[sizeof(float)]; // 4 bytes
                    WriteSingleLittleEndian(bytes, float32);
                    break;
                case long sint64:
                    bytes = new byte[sizeof(long)]; // 8 bytes
                    WriteInt64LittleEndian(bytes, sint64);
                    break;
                case ulong uint64:
                    bytes = new byte[sizeof(ulong)]; // 8 bytes
                    WriteUInt64LittleEndian(bytes, uint64);
                    break;
                case double float64:
                    bytes = new byte[sizeof(double)]; // 8 bytes
                    WriteDoubleLittleEndian(bytes, float64);
                    break;
                // case Int128 sint128:
                //     bytes = new byte[x128bit]; //absolutely massive
                //     WriteInt128LittleEndian(bytes, sint128);
                //     break;
                // case UInt128 uint128:
                //     bytes = new byte[x128bit];
                //     WriteUInt128LittleEndian(bytes, uint128);
                //     break;
                case nint sint32or64:
                    if (IntPtr.Size == x32bit)
                    {
                        bytes = new byte[x32bit];
                        WriteInt32LittleEndian(bytes, (int)sint32or64);
                    }
                    else if (IntPtr.Size == x64bit)
                    {
                        bytes = new byte[x64bit];
                        WriteInt64LittleEndian(bytes, sint32or64);
                    }
                    else
                        throw new PlatformNotSupportedException("Must be 32bit or 64bit process");
                    break;
                case nuint uint32or64:
                    if (!Environment.Is64BitProcess)
                    {
                        bytes = new byte[x32bit];
                        WriteUInt32LittleEndian(bytes, (uint)uint32or64);
                    }
                    else if (Environment.Is64BitProcess)
                    {
                        bytes = new byte[x64bit];
                        WriteUInt64LittleEndian(bytes, uint32or64);
                    }
                    else
                        throw new PlatformNotSupportedException("Must be 32bit or 64bit process");
                    break;
                    // case decimal float128:
                    // bytes = new byte[x128bit];
                    // Write
            }
            if (bytes.Length == 0)
                throw new NotSupportedException("Numeric type not supported by BinaryPrimitives.");
            return bytes;
        }

    }
}
#endif