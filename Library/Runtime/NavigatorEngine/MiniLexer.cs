using System;
using XQuinn.CodeAnalysis;
using XQuinn.Extensions;

namespace XQuinn.Runtime.NavigatorEngine
{

    static class MiniLexer
    {


        internal static bool AssignmentSubstring(string invocation, out string? left, out string? right)
        {
            left = null;
            right = null;
            int? assignment = null;
            for (int i = 0; i < invocation.Length; i++)
            {
                char value = invocation[i];
                if (CharOrString(value))
                    return false;
                if (value == '(')
                    return false;
                if (value == '=')
                {
                    assignment = i;
                    break;
                }
            }
            if (assignment == null)
                return false;
            left = invocation.Remove(assignment.Value);
            right = invocation.Substring(assignment.Value + 1);
            return true;

        }

        internal static string? ResolveMemberAccessOrFloat(string invocation, out string member, out bool field) //returns typename, outputs the accessed member
        {
            int? lastAccessorIndex = null;
            field = true;
            member = invocation;
            char value;
            int index = 0;
            for (int x = 0; x < invocation.Length; x++)
            {
                value = invocation[x];
                if (value != CallLexer.Whitespace)
                {
                    index = x;
                    if (value.IsDigit() || value == '-') //because floats will resolve as member access. lol.
                        return null;
                    else
                        break;
                }
            }
            for (int i = index; i < invocation.Length; i++) //this resolves typenames vs member names, lexer does something similar but not exactly the same - this one is pretty much universal
            {                                        //works with anything like field or method() (no type name) or namespace.typename.method(22, "hello", othertype.method()) //methods are broken off from the typename with all their parameters included
                value = invocation[i];
                if (CharOrString(value))
                {
                    return lastAccessorIndex == null ? null : throw new ArgumentException($"Invalid string format or member name. {invocation}.");
                }
                if (value == '(')
                {
                    field = false;
                    break;
                }
                if (value == '.')
                    lastAccessorIndex = i;
            }
            if (lastAccessorIndex != null)
            {
                member = invocation.Substring(lastAccessorIndex.Value + 1);
                string typename = invocation.Remove(lastAccessorIndex.Value);
                return typename;
            }
            return null;
        }

        internal static bool ImplicitThisMethodCall(string righthand) //makes sure that the '(' contained is not part of a string and is actually a method parameter
        {
            for (int i = 0; i < righthand.Length; i++)
            {
                char value = righthand[i];
                if (CharOrString(value))
                    return false;
                if (value == '(')
                    return true;
            }
            return false;
        }

        static bool CharOrString(char c) => c == '"' || c == '\'';
    }



}
