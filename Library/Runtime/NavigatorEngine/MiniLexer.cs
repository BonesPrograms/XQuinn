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
                        if (CharOrString(value) || value.IsDigit() || value == CallLexer.MemberAccess || value == '-') //because floats will resolve as member access. lol.
                            return null;
                        else
                            break;
                    }
                }
            }
            int? lastAccessorIndex = null;
            for (int i = index; i < stop; i++)
            {
                char c = invocation[i];
                if (c == CallLexer.Whitespace)
                    continue;
                if (c == CallLexer.GenericDeclr)
                {
                    int nest = 0;
                    int lastcount = 0;
                    bool beginNest = false;
                    for (int x = i; x < stop; x++)
                    {
                        c = invocation[x];
                        if (c == CallLexer.GenericDeclr)
                        {
                            if (beginNest)
                            {
                                nest++;
                                lastcount++;
                            }
                            else
                                beginNest = true;
                        }
                        else if (c == CallLexer.GenericTerminate)
                        {
                            if (lastcount > nest)
                                lastcount--;
                            if (lastcount == 0)
                            {
                                i = x;
                                break;
                            }
                            if (nest > 0)
                                nest--;
                        }
                    }
                    if (lastcount > 0)
                        throw new LexicalException("Nested generics not properly terminated. You can fix this by slapping an ext a > on the end, usually.", invocation);
                }
                else if (c == CallLexer.MemberAccess)
                {
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
