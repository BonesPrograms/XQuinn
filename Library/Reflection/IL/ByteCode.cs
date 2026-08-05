#if NET6_0_OR_GREATER
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Numerics;
using XQuinn.Extensions;
using System.ComponentModel;
using XQuinn.Reflection;
using XQuinn.Numerics;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System;

namespace XQuinn.Reflection.IL
{

public static class OpCodeMap
{

    /// <summary>
    /// A dictionary of opcodes that can be indexed by their short value (OpCode.Value).
    /// </summary>
    public static readonly IReadOnlyDictionary<short, OpCode> OpCodes = Map();

    static IReadOnlyDictionary<short, OpCode> Map()
    {
        Dictionary<short,OpCode> dic = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
        .Where(x => x.FieldType == typeof(OpCode))
        .Select(x => (OpCode)x.GetValue(null)!).ToDictionary(x => x.Value, v => v);
        return new ReadOnlyDictionary<short, OpCode>(dic);
    }

}
/// <summary>
/// Readable IL instruction.
/// </summary>
public sealed class ByteCode : MetadataReader
{
#pragma warning disable CA2211 // Non-constant fields should not be visible
    public static bool ShowOpCodeBytes = false;
    public static bool ShowOperandBytes = false;
    public static bool ShowOperandType = false;

#pragma warning restore CA2211 // Non-constant fields should not be visible
    int Offset; //this is kind of fucking useless to end users right now, since this isnt really part of some sort of reflection.emit system. emit.label?
    public OpCode OpCode => _opcode;
    public object? Operand => Object;
    public int? Token => _token;
    public IReadOnlyList<byte>? OperandBytes => _bytesreadonly;
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
    IReadOnlyList<byte>? _bytesreadonly;
    byte[]? _bytes;

    ByteCode? _lastInstruction;
    ByteCode? _nextInstruction;

    ByteCode(object? operand) : base(operand)
    {

    }
    internal static ByteCode New(OpCode opcode, object? operand, int offset, object token)
    {
        ByteCode code = new(operand)
        {
            _opcode = opcode,
            Offset = offset,
            _hasoperand = operand is not null
        };
        if (operand is not null and not LocalVariableInfo and not ParameterInfo)
        {
            if (token is int integer && operand is not ValueType) //i think operand would be a memberinfo here
                code._token = integer;
            if (operand is not sbyte)
            {
                byte[]? bytes = TokenToBytes(token);
                if (bytes != null)
                {
                    code._bytes = bytes.ToArray();
                    code._bytesreadonly = Array.AsReadOnly(code._bytes);
                }
            }
        }
        return code;
    }

    static byte[]? TokenToBytes(object token) => token switch
    {
        short int16 => BytesLittleEndian.AsBytes(int16),
        int int32 => BytesLittleEndian.AsBytes(int32),
        float float32 => BytesLittleEndian.AsBytes(float32),
        long int64 => BytesLittleEndian.AsBytes(int64),
        double float64 => BytesLittleEndian.AsBytes(float64),
        _ => null
    };

    protected override StringBuilder ToStringBuilder()
    {
        StringBuilder sb = new();
        sb.Append($"IL_{Offset:x4}: ");
        sb.Append(OpCode.ToString());
        if (ShowOperandType)
            sb.Append($" {OpCode.OperandType}");
        if (Object is ConstructorInfo)
            sb.Append(" instance void");
        sb.Append(' ');
        OperandToString(sb);
        if ((ShowOpCodeBytes) || (ShowOperandBytes && (Token != null || _bytesreadonly != null)))
            sb.Append(" :: ");
        if (ShowOpCodeBytes)
        {
            sb.Append("OpCodeBytes: ");
            sb.Append(BytesToString(BytesLittleEndian.AsBytes(OpCode.Value)));
        }
        if (_bytesreadonly != null && ShowOperandBytes)
        {
            if (Token != null)
                sb.Append($"Token {Token} ");
            sb.Append("Bytes ");
            sb.Append(BytesToString(_bytes!));
        }
        return sb;
    }
    static StringBuilder BytesToString(byte[] bytes)
    {
        StringBuilder sb = new();
        for (int i = 0; i < bytes.Length; i++)
            sb.Append($"{bytes[i]} ");
        return sb;
    }
    void OperandToString(StringBuilder sb)
    {
        if (Operand is LocalVariableInfo lvar)
        {
            Type type = lvar.LocalType;
            sb.Append($"{GenericTypeToString(type)} ({lvar.LocalIndex})");
        }
        else if (Operand is ParameterInfo info)
        {
            Type type = info.ParameterType;
            sb.Append(GenericTypeToString(type) + $" {info.Name}");
        }
        else if (OpCode.OperandType == OperandType.InlineBrTarget || OpCode.OperandType == OperandType.ShortInlineBrTarget)
        {
            int num = 0;
            if (Operand is sbyte bits)
                num = bits;
            else if (Operand is int integer)
                num = integer;
            sb.Append($"IL_{num:x4}");
        }
        else if (Object is string)
            sb.Append($"\"{Object}\"");
        else
            sb.Append(base.ToStringBuilder());
    }
}
}
#endif