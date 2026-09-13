using System;
using System.Text;
using XQuinn.CodeAnalysis;

namespace XQuinn.Private.LexicalEngine
{
    abstract class LexicalObject
    {
        protected InvokeLexer _lexer;
        protected StringBuilder _sb => _lexer._sb;
        protected char _value { get => _lexer._value; set => _lexer._value = value; }
        protected void SkipWhitespaceTrail(ref int i, string invoc) => _lexer.SkipWhitespaceTrail(ref i, invoc);
        public LexicalObject(InvokeLexer lexer)
        {
            _lexer = lexer;
        }
    }
}