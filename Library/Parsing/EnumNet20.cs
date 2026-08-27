using System;

namespace XQ.Parsing
{

    //modern tryparse for older net versions such as net standard 2.0
    public static class EnumNet20
    {
        public static bool TryParse(string String, Type enumType, bool ignoreCase, out Enum? @enum)
        {
            @enum = null;
            try
            {
                @enum = Enum.Parse(enumType, String, ignoreCase) as Enum;
            }
            catch
            {

            }
            return @enum != null;
        }
    }
}