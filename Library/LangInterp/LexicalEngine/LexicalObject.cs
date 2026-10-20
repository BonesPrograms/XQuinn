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
        protected CallLexer Lexer { get; init; }
        protected bool WhitespaceEnd() => CallLexer.Termination(Lexer.Character) || ((Lexer.ReadQualifiedMember || Lexer.ArbitraryReader.BeganReadingMainmethodName || Lexer.ReadArbitraryLegal) && Lexer.Character == CallLexer.MethodDeclr);
        public LexicalObject(CallLexer lexer)
        {
            Lexer = lexer;
        }
    }
}
