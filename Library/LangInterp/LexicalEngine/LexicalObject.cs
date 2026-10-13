namespace XQuinn.LangInterp.LexicalEngine
{
    enum ControlFlow
    {
        Increment,

        Append,

        Release
    }
    abstract class LexicalObject
    {
        protected CallLexer _lexer;
        protected bool WhitespaceEnd() => CallLexer.Termination(_lexer._curr_char) || ((_lexer._read_qualified_member || _lexer._arbitrary_reader._beganReadingMainMethodName || _lexer._read_arbitrary_legal_value) && _lexer._curr_char == CallLexer.MethodDeclr);
        public LexicalObject(CallLexer lexer)
        {
            _lexer = lexer;
        }
    }
}
