using System;
using System.Text;
using XQuinn.CodeAnalysis;

namespace XQuinn.CodeAnalysis.LexicalEngine
{
    abstract class LexicalObject
    {
        protected InvokeLexer _lexer;
        protected StringBuilder _sb => _lexer._sb;
        protected char _value { get => _lexer._value; set => _lexer._value = value; }
        internal void SkipWhitespaceTrail(ref int i, string invocation)
        {
            while (i < invocation.Length)
            {
                i++;
                _value = invocation[i];
                if (InvokeLexer.Termination(_value) || ((_lexer._readQualifiedMember || _lexer._beganReadingMainMethodName || _lexer._readArbitraryLegalValue) && _value == InvokeLexer.MethodStart))
                    return;
                if (_value != InvokeLexer.Whitespace)
                    throw new LexicalException("Detected trailing input after whitespace.", invocation, _value, _sb, i);
            } 
        }
        public LexicalObject(InvokeLexer lexer)
        {
            _lexer = lexer;
        }
    }
}