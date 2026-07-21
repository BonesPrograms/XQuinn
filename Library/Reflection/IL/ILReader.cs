using System.Reflection.Emit;
using System.Reflection;
using System.Buffers.Binary;
using static XQuinn.Numerics.BytesLittleEndian;
using System.Collections.Immutable;
using XQuinn.IO;

namespace XQuinn.Reflection.IL;

//Todo: Reading delegates, reading delegate bodies?


//learn wtf an offset is in IL

//Update:
//This has gotten pretty good now, the only thing it cant really read yet are jump labels. Otherwise it is able to read shit pretty much just as good as the MonoCecil ILReader

/// <summary>
/// Converts a method into readable IL instructions. 
/// </summary>
public sealed class ILReader
{
    public readonly byte[] IL;
    public readonly IList<LocalVariableInfo> Locals;
    public readonly ParameterInfo[] Params;
    readonly Module Module;
    readonly MethodInfo Method;
    /// <summary>
    /// Some opcodes are 2 bytes long, they will always start with a "prefix" byte.
    /// </summary>
    const byte PrefixBit = 0xFE;
    readonly Type[]? GenericMethodArgs;
    readonly Type[]? GenericTypeArgs;
    ByteCode? LastInstruction;
    public ILReader(MethodInfo method)
    {
        MethodBody body = method.GetMethodBody() ?? throw new ArgumentException("Method body is null.");
        IL = body.GetILAsByteArray() ?? throw new ArgumentException("Byte array is null.");
        Locals = body.LocalVariables;
        Params = method.GetParameters();
        Module = method.Module;
        Method = method;
        GenericMethodArgs = method.GetGenericArguments();
        GenericTypeArgs = method.DeclaringType?.GetGenericArguments();
    }

    // public string ReadIL()
    // {
    //     StringBuilder sb = new();
    // }

    /// <summary>
    /// Prints readable IL to a file.
    /// </summary>
    /// <param name="path"></param>
    public void PrintIL(string path) //sho
    {
        List<ByteCode> codes = GetIL();
        Write.SafetyCheck(path);
        using StreamWriter writer = new(path);
        writer.WriteLine($".method");
        writer.WriteLine("	" + MetadataReader.MethodToString(Method, true)); //,maybe should edit the stringbuilder to slip in parameter names
        writer.WriteLine("{");
        writer.WriteLine("");
          //  writer.WriteLine("	.maxstack 1");
        writer.WriteLine("	.locals init (");
        for (int i = 0; i < Locals.Count; i++)
        {
            bool val = Locals.Count > 1 && i != Locals.Count - 1;
            char? c = val ? ',' : null;
            writer.WriteLine($"		[{i}] {Locals[i].LocalType.Name.ToLower()}{c}");

        }
        writer.WriteLine("	)");
        writer.WriteLine("");
        foreach (ByteCode code in codes)
        {
            writer.WriteLine("	" + code.ToString());
        }
        writer.WriteLine("}");
    }

    /// <summary>
    /// Returns a list of readable IL.
    /// </summary>
    /// <returns></returns>
    public List<ByteCode> GetIL()
    {
        int i = 0;
        List<ByteCode> codes = [];
        while (i < IL.Length)
            BytesToIL(codes, ref i);
        return codes;
    }

    void BytesToIL(List<ByteCode> codes, ref int i)
    {
        OpCode code = GetOpCode(ref i);
        int size = OperandSize(code.OperandType);
        object token = GetToken(i, ref size, code.OperandType);
        object? operand = size == x0bit ? null : GetOperand(code, token, i);
        ByteCode instruction = new(code, operand, i - 1, token);
        codes.Add(instruction); //for some reason it is off by 1, you want to shift the offset back by 1 or every label will be off by 1 individually and cumulatively so all labels will be off
        i += size;
        instruction.LastInstruction = LastInstruction;
        LastInstruction?.NextInstruction = instruction;
        LastInstruction = instruction;
    }

    OpCode GetOpCode(ref int i)
    {
        OpCode code;
        byte indexedbyte = IL[i];
        if (indexedbyte == PrefixBit)
        {
            byte nextbyte = IL[i + 1];
            //short key = BinaryPrimitives.ReadInt16LittleEndian(IL.AsSpan(i, x16bit));
            short key = (short)((PrefixBit << 0x08) | nextbyte); //learn how to do bitwise!
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

    //This needs to return an object for the token, because the token is sometimes just a numeric value for a struct and not an actual 32bit metadata token
    //(in other words, it is the operand)

    //First off, if you have a double or a float, you will lose floating point precision if you do not return it as anything less than a double.
    //Second off, if you have a long, you will lose integer precision if you return it as a double

    //A decimal is 128 bits, but still cannot fully maintain integer precision, floating point precision, range and magnitude
    //of other integral numeric types (such as a double or float) that are cast to decimal, because raw byte count isn't enough to get the job done

    //I should note - actual Int128 structs will be readable until they reach around 19-20 digits (a long can only hold 19 digits)
    //So far, this cannot be remedied - there is no 128 bit operand type, and even real decompilers (like ilspy)
    //will not properly decompile the UInt128 number 11111111111111111111, for example. Becaue ilspy doesnt do it, i wont do it either, lol!

    //That being said, decimal values are also very unreadable, but you will see how that works in IL
    object GetToken(int i, ref int size, OperandType type)
    {
        if (size == x0bit)
            return x0bit; //no operand
        else if (size == x8bit)
            return IL[i];
        else if (size == x16bit)
            return BinaryPrimitives.ReadInt16LittleEndian(IL.AsSpan(i, x16bit)); //BitConverter works, but BinaryPrimitives will automatically handle it without us
        else if (size == x32bit)                                       //having to reverse the bytes to get their token on BigEndian systems
        {                                                                       //IL is always stored as little endian, but at runtime could be bigendian
            int token;
            if (type == OperandType.ShortInlineR)
                return BinaryPrimitives.ReadSingleLittleEndian(IL.AsSpan(i, x32bit));
            else
                token = BinaryPrimitives.ReadInt32LittleEndian(IL.AsSpan(i, x32bit)); ;
            if (type == OperandType.InlineSwitch)
                size += token * 4;
            return token;
        }
        else if (size == x64bit)
        {
            if (type == OperandType.InlineR)
                return BinaryPrimitives.ReadDoubleLittleEndian(IL.AsSpan(i, x64bit));
            return BinaryPrimitives.ReadInt64LittleEndian(IL.AsSpan(i, x64bit));
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
