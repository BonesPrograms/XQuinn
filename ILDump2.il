.method
	instance Int32 Expression::Expr2(Int32 num1, Int32 num2, Int32 num3, Int32 num4, Int32 num5)
{

	.locals init (
		[0] int32
	)

	IL_0000: nop 
	IL_0001: ldarg.1 
	IL_0002: ldarg.2 
	IL_0003: ldarg.3 
	IL_0004: add 
	IL_0005: ldarg.s Int32 num4
	IL_0007: mul 
	IL_0008: add 
	IL_0009: ldarg.s Int32 num5
	IL_000b: add 
	IL_000c: stloc.0 
	IL_000d: br.s IL_000f
	IL_000f: ldloc.0 
	IL_0010: ret 
}


16 - 8 = 8;
16 - 5 = 11