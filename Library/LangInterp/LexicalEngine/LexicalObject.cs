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
        protected CallLexer Lexer { get;}
        public LexicalObject(CallLexer lexer)
        {
            Lexer = lexer;
        }
    }
}
