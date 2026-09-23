using System;
using System.Text;
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
            int stop = invocation.IndexOf('(');
            field = stop < 0;
            stop = field ? invocation.Length : stop;
            int index = 0;
            member = invocation;
            if (field)
            {
                for (int x = 0; x < invocation.Length; x++)
                {
                    char value = invocation[x];
                    if (value != CallLexer.Whitespace)
                    {
                        index = x;
                        if (CharOrString(value) || value.IsDigit() || value == '-') //because floats will resolve as member access. lol.
                            return null;
                        else
                            break;
                    }
                }
            }
            int? lastAccessorIndex = null;
            bool isgeneric = false;
            for (int i = index; i < stop; i++)
            {
                char c = invocation[i];
                if (c == CallLexer.Whitespace)
                    continue;
                if (c == CallLexer.GenericDeclr)
                    isgeneric = true;
                if (c == CallLexer.MemberAccess)
                {
                    bool skip = false;
                    if (isgeneric)
                    {
                        for (int x = i; x >= 0; x--)
                        {
                            char z = invocation[x];
                            if (z == CallLexer.GenericTerminate)
                                break;
                            if (z == CallLexer.GenericDeclr)
                            {
                                skip = true;
                                break;
                            }
                        }
                        if (!skip)
                            for (int x = i; x < stop; x++)
                            {
                                char z = invocation[x];
                                if (z == CallLexer.GenericDeclr)
                                    break;
                                if (z == CallLexer.GenericTerminate)
                                {
                                    skip = true;
                                    break;
                                }
                            }
                    }
                    if (!skip)
                        lastAccessorIndex = i;
                }

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
