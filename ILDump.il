.method
	instance Int32 Expression::Expr()
{

	.locals init (
		[0] int32,
		[1] int32
	)

	IL_0000: nop 
	IL_0001: ldc.i4.s 26
	IL_0003: stloc.0 
	IL_0004: ldloc.0 
	IL_0005: stloc.1 
	IL_0006: br.s IL_0008
	IL_0008: ldloc.1 
	IL_0009: ret 
}
