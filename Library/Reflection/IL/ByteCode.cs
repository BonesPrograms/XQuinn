using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Collections.Immutable;
using System.Numerics;
using XQuinn.Extensions;
using System.ComponentModel;
using XQuinn.Reflection;
using XQuinn.Numerics;

namespace XQuinn.Reflection.IL;

public static class OpCodeMap
{

    /// <summary>
    /// A dictionary of opcodes that can be indexed by their short value (OpCode.Value).
    /// </summary>
    public static readonly ImmutableDictionary<short, OpCode> OpCodes =
    typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
    .Where(x => x.FieldType == typeof(OpCode))
    .Select(x => (OpCode)x.GetValue(null)!)
    .ToImmutableDictionary(k => k.Value, v => v);
}
/// <summary>
/// Readable IL instruction.
/// </summary>
public sealed class ByteCode : MetadataReader
{
#pragma warning disable CA2211 // Non-constant fields should not be visible
    public static bool ShowOpCodeBytes =  false;
    public static bool ShowOperandBytes = false;
    public static bool ShowOperandType = false;

#pragma warning restore CA2211 // Non-constant fields should not be visible
    readonly int Offset; //this is kind of fucking useless to end users right now, since this isnt really part of some sort of reflection.emit system. emit.label?
    public readonly OpCode OpCode;
    public object? Operand => Object;
    public readonly int? Token; //MetadataToken
    public readonly IReadOnlyList<byte>? OperandBytes;
    readonly byte[]? _bytes;
    public readonly bool HasOperand; //this does not mean that OperandBytes is not null, it just means this opcode has an operand - if bytes are null, this implies a single byte operand
                                     //or a local variable / paramete
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
    ByteCode? _lastInstruction;
    ByteCode? _nextInstruction;
    internal ByteCode(OpCode opcode, object? operand, int offset, object token) : base(operand)
    {
        OpCode = opcode;
        Offset = offset;
        HasOperand = operand is not null;
        if (operand is not null and not LocalVariableInfo and not ParameterInfo)
        {
            if (token is int integer && Operand is not ValueType) //i think operand would be a memberinfo here
                Token = integer;
            if (Operand is not sbyte)
            {
                byte[]? bytes = TokenToBytes(token);
                if (bytes != null)
                {
                    _bytes = [.. bytes];
                    OperandBytes = _bytes.AsReadOnly();
                }
            }
        }
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
        if ((ShowOpCodeBytes) || (ShowOperandBytes && (Token != null || OperandBytes != null)))
            sb.Append(" :: ");
        if (ShowOpCodeBytes)
        {
            sb.Append("OpCodeBytes: ");
            sb.Append(BytesToString(BytesLittleEndian.AsBytes(OpCode.Value)));
        }
        if (OperandBytes != null && ShowOperandBytes)
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
            sb.Append($"{CheckTypeGenerics(type)} ({lvar.LocalIndex})");
        }
        else if (Operand is ParameterInfo info)
        {
            Type type = info.ParameterType;
            sb.Append(CheckTypeGenerics(type) + $" {info.Name}");
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