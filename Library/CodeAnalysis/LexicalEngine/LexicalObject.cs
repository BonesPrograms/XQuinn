using System;
using System.Text;
using XQuinn.CodeAnalysis;

namespace XQuinn.CodeAnalysis.LexicalEngine
{
    abstract class LexicalObject
    {
        protected CallLexer Lexer;
        internal void SkipWhitespaceTrail(ref int i, string invocation)
        {
            while (i < invocation.Length)
            {
                i++;
                Lexer.CurrentValue = invocation[i];
                if (WhitespaceEnd())
                    return;
                if (Lexer.CurrentValue != CallLexer.Whitespace)
                    throw new LexicalException("Detected trailing input after whitespace.", invocation, Lexer.CurrentValue, Lexer.Writer, i);
            } 
        }

       protected bool WhitespaceEnd() => CallLexer.Termination(Lexer.CurrentValue) || ((Lexer.ReadQualifiedMember || Lexer.ArbitraryReader.BeganReadingMainMethodName || Lexer.ReadArbitraryLegalValue) && Lexer.CurrentValue == CallLexer.MethodDeclr);
        public LexicalObject(CallLexer lexer)
        {
            Lexer = lexer;
        }
    }
}