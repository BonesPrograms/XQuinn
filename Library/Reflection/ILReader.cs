#if NET6_0_OR_GREATER
using System.Reflection.Emit;
using System.Reflection;
using System.Buffers.Binary;
using static XQuinn.Numerics.ByteSizes;
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
using XQuinn.Numerics;
using System.Linq;

namespace XQuinn.Reflection.IL
{

    //Todo: Reading delegates, reading delegate bodies?


    //learn wtf an offset is in IL

    //Update:
    //This has gotten pretty good now, the only thing it cant really read yet are jump labels. Otherwise it is able to read shit pretty much just as good as the MonoCecil ILReader

    /// <summary>
    /// Converts a method into readable IL instructions. 
    /// </summary>
    public sealed class ILReader
    {
        public static class OpCodeMap
        {

            /// <summary>
            /// A dictionary of opcodes that can be indexed by their short value (OpCode.Value).
            /// </summary>
            public static readonly IReadOnlyDictionary<short, OpCode> OpCodes = Map();

            static ReadOnlyDictionary<short, OpCode> Map()
            {
                Dictionary<short, OpCode> dic = typeof(OpCodes)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(x => x.FieldType == typeof(OpCode))
                .Select(x => (OpCode)x.GetValue(null)!).ToDictionary(x => x.Value, v => v);
                return new ReadOnlyDictionary<short, OpCode>(dic);
            }

        }
        /// <summary>
        /// Readable IL instruction.
        /// </summary>
        public sealed class ByteCode : MetadataPrinter
        {
#if NET6_0_OR_GREATER
#pragma warning disable CA2211 // Non-constant fields should not be visible
            public static bool ShowOpCodeBytes = false;
            public static bool ShowOperandBytes = false;
#endif
            public static bool ShowOperandType = false;

#pragma warning restore CA2211 // Non-constant fields should not be visible

            int Offset; //this is kind of fucking useless to end users right now, since this isnt really part of some sort of reflection.emit system. emit.label?
            public OpCode OpCode => _opcode;
            public object? Operand => Object;
            public int? Token => _token;
#if NET6_0_OR_GREATER
            public IReadOnlyList<byte>? OperandBytes => _bytesreadonly;
#endif
            public bool HasOperand => _hasoperand; //this does not mean that OperandBytes is not null, it just means this opcode has an operand - if bytes are null, this implies a single byte operand                                                                         //or a local variable / paramete
            public ByteCode? LastInstruction
            {
                get => _lastInstruction;
                internal set
                {
                    _lastInstruction ??= value;
                }
            }

            public ByteCode? NextInstruction
            {
                get => _nextInstruction;
                internal set
                {
                    _nextInstruction ??= value;
                }
            }
            bool _hasoperand;
            OpCode _opcode;
            int? _token; //MetadataToken
#if NET6_0_OR_GREATER
            IReadOnlyList<byte>? _bytesreadonly;
            byte[]? _bytes;
#endif

            ByteCode? _lastInstruction;
            ByteCode? _nextInstruction;

            ByteCode(object? operand) : base(operand)
            {

            }
            public static ByteCode New(OpCode opcode, object? operand, int offset, object token)
            {
                ByteCode code = new(operand)
                {
                    _opcode = opcode,
                    Offset = offset,
                    _hasoperand = operand is not null
                };
                if (operand is not null and not LocalVariableInfo and not ParameterInfo)
                {
                    if (token is int integer && operand is not ValueType) code._token = integer;
#if NET6_0_OR_GREATER
                    if (operand is not sbyte)
                    {
                        byte[]? bytes = TokenToBytes(token);
                        if (bytes != null) { code._bytes = bytes.ToArray(); code._bytesreadonly = Array.AsReadOnly(code._bytes); }
                    }
#endif
                }
                return code;
            }
#if NET6_0_OR_GREATER
            static byte[]? TokenToBytes(object token) => token switch
            {
                short int16 => BytesLittleEndian.AsBytes(int16),
                int int32 => BytesLittleEndian.AsBytes(int32),
                float float32 => BytesLittleEndian.AsBytes(float32),
                long int64 => BytesLittleEndian.AsBytes(int64),
                double float64 => BytesLittleEndian.AsBytes(float64),
                _ => null
            };
#endif
            protected override StringBuilder ToStringBuilder()
            {
                StringBuilder sb = new();
                sb.Append($"IL_{Offset:x4}: ");
                sb.Append(OpCode.ToString());
                if (ShowOperandType) sb.Append($" {OpCode.OperandType}");
                if (Object is ConstructorInfo) sb.Append(" instance void");
                sb.Append(' ');
                OperandToString(sb);
#if NET6_0_OR_GREATER
                if (ShowOpCodeBytes || (ShowOperandBytes && (Token != null || _bytesreadonly != null)))
                    sb.Append(" :: ");
                if (ShowOpCodeBytes)
                {
                    sb.Append("OpCodeBytes: ");
                    BytesToString(sb, BytesLittleEndian.AsBytes(OpCode.Value));
                }
                if (_bytesreadonly != null && ShowOperandBytes)
                {
                    if (Token != null) sb.Append($"Token {Token} ");
                    sb.Append("Bytes ");
                    BytesToString(sb, _bytes!);
                }
#endif
                return sb;
            }
#if NET6_0_OR_GREATER
            static void BytesToString(StringBuilder sb, byte[] bytes)
            {
                for (int i = 0; i < bytes.Length; i++) sb.Append($"{bytes[i]} ");
            }
#endif
            void OperandToString(StringBuilder sb)
            {
                if (Operand is LocalVariableInfo lvar) { GenericTypeToString(sb, lvar.LocalType); sb.Append($" {lvar.LocalIndex}"); }
                else if (Operand is ParameterInfo info) { GenericTypeToString(sb, info.ParameterType); sb.Append($" {info.Name}"); }
                else if (OpCode.OperandType == OperandType.InlineBrTarget || OpCode.OperandType == OperandType.ShortInlineBrTarget)
                {
                    if (Operand == null) throw new InvalidOperationException();
                    int num = (int)Operand;
                    sb.Append($"IL_{num:x4}");
                }
                else if (Object is string) sb.Append($"\"{Object}\"");
                else sb.Append(base.ToStringBuilder());
            }
        }
        public IReadOnlyList<byte> MSIL => _msil;
        public IReadOnlyList<LocalVariableInfo> Locals => _locals;
        public IReadOnlyList<ParameterInfo> Params => _params;
        IReadOnlyList<byte> _msil = null!;
        IReadOnlyList<LocalVariableInfo> _locals = null!;
        IReadOnlyList<ParameterInfo> _params = null!;
        byte[] _il = null!;
        readonly Module Module;
        readonly MethodInfo Method;

        /// <summary>
        /// Some opcodes are 2 bytes long, they will always start with a "prefix" byte.
        /// </summary>
        const byte PrefixBit = 0xFE;
        Type[]? GenericMethodArgs;
        Type[]? GenericTypeArgs;
        ByteCode? LastInstruction;
        ILReader(MethodInfo method)
        {
            Method = method;
            Module = method.Module;
        }

        public static List<ByteCode> GetIL(MethodInfo method)
        {
            return New(method).GetIL();
        }
        public static void PrintIL(MethodInfo method, string outputFilePath, bool makeFileIfNotFound)
        {
            New(method).PrintIL(outputFilePath, makeFileIfNotFound);
        }

        public static ILReader New(MethodInfo method)
        {
            if (Harmony.GetPatchInfo(method) != null)
                throw new NotSupportedException($"{method} in {method.DeclaringType} has been patched by harmony and it's actual behavior cannot properly be represented by ILReader.");
            MethodBody body = method.GetMethodBody() ?? throw new ArgumentException("Method body is null.");
            byte[] il = body.GetILAsByteArray() ?? throw new ArgumentException("Byte array is null.");
            return new(method)
            {
                _il = il,
                _msil = Array.AsReadOnly(il),
                _locals = new ReadOnlyCollection<LocalVariableInfo>(body.LocalVariables),
                _params = Array.AsReadOnly(method.GetParameters()),
                GenericMethodArgs = method.GetGenericArguments(),
                GenericTypeArgs = method.DeclaringType?.GetGenericArguments()
            };
        }

        // public string ReadIL()
        // {
        //     StringBuilder sb = new();
        // }

        /// <summary>
        /// Prints readable IL to a file.
        /// </summary>
        /// <param name="outputFilePath"></param>
        public void PrintIL(string outputFilePath, bool makeFileIfNotFound) //sho
        {
            List<ByteCode> codes = GetIL();
            if (makeFileIfNotFound)
                XQuinn.IO.Logger.SafetyCheck(outputFilePath);
            using StreamWriter writer = new(outputFilePath);
            writer.WriteLine($"method");
            writer.WriteLine("	" + MetadataPrinter.MethodToString(Method, true)); //,maybe should edit the stringbuilder to slip in parameter names
            writer.WriteLine("");
            //  writer.WriteLine("	.maxstack 1");
            writer.WriteLine("Locals");
            for (int i = 0; i < Locals.Count; i++)
            {
                bool needcomma = For.Multiples(Locals.Count, i);
                char? comma = needcomma ? ',' : null;
                writer.WriteLine($"		[{i}] {Locals[i].LocalType.Name}{comma}");

            }
            writer.WriteLine("");
            foreach (ByteCode code in codes) writer.WriteLine("	" + code.ToString());
        }

        /// <summary>
        /// Returns a list of readable IL.
        /// </summary>
        /// <returns></returns>
        public List<ByteCode> GetIL()
        {
            int i = 0;
            List<ByteCode> codes = new();
            while (i < _il.Length) BytesToIL(codes, ref i);
            return codes;
        }

        void BytesToIL(List<ByteCode> codes, ref int i)
        {
            OpCode code = GetOpCode(ref i);
            int size = OperandSize(code.OperandType);
            object token = GetToken(i, ref size, code.OperandType);
            object? operand = size == x0bit ? null : GetOperand(code, token, i);
            ByteCode instruction = ByteCode.New(code, operand, i - 1, token);
            codes.Add(instruction); //for some reason it is off by 1, you want to shift the offset back by 1 or every label will be off by 1 individually and cumulatively so all labels will be off
            i += size;
            instruction.LastInstruction = LastInstruction;
            if (LastInstruction != null) LastInstruction.NextInstruction = instruction;
            LastInstruction = instruction;
        }

        OpCode GetOpCode(ref int i)
        {
            OpCode code;
            byte indexedbyte = _il[i];
            if (indexedbyte == PrefixBit)
            {
                byte nextbyte = _il[i + 1];
                short key = (short)((PrefixBit << 0x08) | nextbyte);
                code = OpCodeMap.OpCodes[key];
                i += 2;
            }
            else
            {
                code = OpCodeMap.OpCodes[indexedbyte];
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
                OperandType.InlineMethod => Module.ResolveMethod((int)token, GenericTypeArgs, GenericMethodArgs),
                OperandType.InlineField => Module.ResolveField((int)token, GenericTypeArgs, GenericMethodArgs),
                OperandType.InlineType => Module.ResolveType((int)token, GenericTypeArgs, GenericMethodArgs),
                OperandType.InlineString => Module.ResolveString((int)token),
                OperandType.InlineTok => Module.ResolveMember((int)token, GenericTypeArgs, GenericMethodArgs),
                OperandType.InlineSig => Module.ResolveSignature((int)token),
                OperandType.InlineNone or OperandType.InlineSwitch => null, //the "token" is the jump target, and will carry over for display in ByteCode, so operand is unecessary
                OperandType.InlineBrTarget => (int)token + i + 1, //idk why u need to do +1 but it is always -1 instruction off from its actual jump target
                _ => throw new InvalidProgramException("Operand type does not exist.")
            };

        object GetVariable(OpCode opcode, int token)
        {
            if (opcode.Name?.Contains("loc") ?? false)
            {
                return Locals[token];
            }
            else
            {
                if (Method.IsStatic)
                {
                    return Params[token];
                }
                else if (token == 0) //idk why a token of 0 == this, but i guess that is the byte code for this, wonder where i can learn that
                    return "this"; //idk how to get this, was trying to research it monocecil, but cant access the class they use to resolve waht this is
                return Params[token - 1]; //not sure why they do -1 here, not sure why i do +1 for the jump label lol
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
}
#endif