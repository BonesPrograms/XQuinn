using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace XQuinn.Parsing
{

    //To allow Nint/NUint tryparse on older net versions
    //Currently only for strings though
    public abstract class NativeIntNet20
    {
        public static readonly bool Is64Bit;
        private protected NativeIntNet20() { }
        static NativeIntNet20() { unsafe { Is64Bit = sizeof(nint) == sizeof(long); } }

    }
    public sealed class NIntNet20 : NativeIntNet20
    {
        NIntNet20() { }
        public static bool TryParse(string value, out nint num)
        {
            num = default;
            if (Is64Bit) { if (long.TryParse(value, out long int64)) { num = (nint)int64; return true; } }
            else if (int.TryParse(value, out int int32)) { num = int32; return true; }
            return false;
        }
    }

    public sealed class NUIntNet20 : NativeIntNet20
    {
        NUIntNet20() { }
        public static bool TryParse(string value, out nuint num)
        {
            num = default;
            if (Is64Bit) { if (ulong.TryParse(value, out ulong uint64)) { num = (nuint)uint64; return true; } }
            else if (uint.TryParse(value, out uint uint32)) { num = uint32; return true; }
            return false;
        }

    }
}