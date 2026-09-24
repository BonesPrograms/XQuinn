using System;
using System.Text;
using XQuinn.CodeAnalysis;

namespace XQuinn.CodeAnalysis.LexicalEngine
{
    abstract class LexicalObject
    {
        protected CallLexer _lexer;
        internal void SkipWhitespaceTrail(ref int i, string invocation)
        {
            while (i < invocation.Length)
            {
                i++;
                _lexer._curr_char = invocation[i];
                if (WhitespaceEnd())
                    return;
                if (_lexer._curr_char != CallLexer.Whitespace)
                    throw new LexicalException("Detected trailing input after whitespace.", invocation, _lexer._curr_char, _lexer._writer, i);
            } 
        }

       protected bool WhitespaceEnd() => CallLexer.Termination(_lexer._curr_char) || ((_lexer._read_qualified_member || _lexer._arbitrary_reader._beganReadingMainMethodName || _lexer._read_arbitrary_legal_value) && _lexer._curr_char == CallLexer.MethodDeclr);
        public LexicalObject(CallLexer lexer)
        {
            _lexer = lexer;
        }
    }
}