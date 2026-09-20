using System;
using System.Text;
using XQuinn.CodeAnalysis;

namespace XQuinn.CodeAnalysis.LexicalEngine
{
    abstract class LexicalObject
    {
        protected CallLexer _lexer;
        protected StringBuilder _sb => _lexer._sb;
        protected char _value { get => _lexer._value; set => _lexer._value = value; }
        internal void SkipWhitespaceTrail(ref int i, string invocation)
        {
            while (i < invocation.Length)
            {
                i++;
                _value = invocation[i];
                if (WhitespaceEnd())
                    return;
                if (_value != CallLexer.Whitespace)
                    throw new LexicalException("Detected trailing input after whitespace.", invocation, _value, _sb, i);
            } 
        }

       protected bool WhitespaceEnd() => CallLexer.Termination(_value) || ((_lexer._readQualifiedMember || _lexer._beganReadingMainMethodName || _lexer._readArbitraryLegalValue) && _value == CallLexer.MethodDeclr);
        public LexicalObject(CallLexer lexer)
        {
            _lexer = lexer;
        }
    }
}