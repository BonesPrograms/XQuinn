using System.Collections.Generic;
using XQuinn.LangInterp;

namespace XQuinn.Runtime.NavigatorEngine
{
    static class MiniLexer
    {


        internal static void Autochain(List<string> chain, string invocation)
        {
            chain.Clear();
            bool readstr = false;
            bool noesc = false;
            int lastbreak = 0;
            invocation = $"{invocation};";
            for (int i = 0; i < invocation.Length; i++)
            {
                char c = invocation[i];
                if (!readstr)
                {
                    if (c == CallLexer.Break)
                    {
                        string section = invocation.Substring(lastbreak, i - lastbreak);
                        chain.Add(section);
                        lastbreak = i + 1;
                    }
                    if (c == CallLexer.CharDeclr)
                    {
                        i = i + 2;
                        c = invocation[i];
                        if (c != CallLexer.CharDeclr)
                            throw new LexicalException("Invalid char format", invocation);
                        continue;
                    }
                    if (c == CallLexer.NoEscDeclr)
                    {
                        i++;
                        c = invocation[i];
                        noesc = true;
                        if (c != CallLexer.StringDeclr)
                            throw new LexicalException("@ must be followed immediately by a quote when declaring a string.", invocation);
                    }
                }
                if (c == CallLexer.EscSeq && readstr && !noesc)
                {
                    i++;
                    continue;
                }
                if (c == CallLexer.StringDeclr)
                {
                    if (readstr)
                    {
                        noesc = false;
                        readstr = false;
                    }
                    else
                        readstr = true;
                }
            }

        }
        internal static bool AssignmentSubstring(string invocation, out string? left, out string? right)
        {
            left = null;
            right = null;
            int? assignment = null;
            for (int i = 0; i < invocation.Length; i++)
            {
                char value = invocation[i];
                if (value == CallLexer.MethodDeclr)
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

        internal static string? ResolveMemberAccessOrLiteral(string invocation, out string member, out bool notMethod)
        {
            member = invocation;
            int? lastAccessorIndex = null;
            notMethod = true;
            for (int i = 0; i < invocation.Length; i++)
            {
                char value = invocation[i];
                if (value != CallLexer.Whitespace)
                {
                    if (IsLiteral(value))
                        return null;
                    else
                        break;
                }
            }

            for (int i = 0; i < invocation.Length; i++)
            {
                char c = invocation[i];
                if (c == CallLexer.MethodDeclr)
                {
                    notMethod = false;
                    break;
                }
                if (c == CallLexer.GenericDeclr)
                {
                    int nest = 0;
                    int lastcount = 0;
                    bool beginNest = false;
                    for (int x = i; x < invocation.Length; x++)
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
                else if (c == CallLexer.MemberAccessOrDecimal)
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

        internal static bool ImplicitThisMethodCall(string righthand)
        {
            for (int i = 0; i < righthand.Length; i++)
            {
                char value = righthand[i];
                if (IsLiteral(value))
                    return false;
                if (value == CallLexer.MethodDeclr)
                    return true;
            }
            return false;
        }

        static bool IsLiteral(char value) =>
        value switch
        {
            CallLexer.StringDeclr or CallLexer.CharDeclr or CallLexer.MemberAccessOrDecimal or CallLexer.NegSign or CallLexer.NoEscDeclr => true,
            _ => char.IsDigit(value)
        };
    }
}
