using static XQuinn.LangInterp.CallLexer;
using XQuinn.LangInterp.SyntaxTree;

namespace XQuinn.LangInterp.LexicalEngine
{
    sealed class ValueReader : LexicalObject
    {
        internal bool NoEscape { get; set; }
        internal bool ReadNoEscDeclr { get; set; }
        internal bool ReadORDelimit { get; set; }
        internal bool ReadORVal { get; set; }
        internal bool ReadFloat { get; set; }
        bool FinishedReadChar { get; set; }
        bool ReadCharValue { get; set; }
        bool StringEnding { get; set; }
        bool Escaping { get; set; }
        bool JustEscaped { get; set; }

        internal void Clear()
        {
            ReadFloat = false;
            FinishedReadChar = false;
            ReadCharValue = false;
            StringEnding = false;
            NoEscape = false;
            ReadNoEscDeclr = false;
            Escaping = false;
            JustEscaped = false;
            ReadORDelimit = false;
            ReadORVal = false;
        }

        public ValueReader(CallLexer lexer) : base(lexer)
        {
        }
        internal void ReadParam()
        {
            string prm = Lexer.Buffer.ToString();
            ValueString param = new(prm);
            Lexer.Buffer.Length = 0;
            Lexer.CurrentMethod!.AddParameter(param);
        }


        internal ControlFlow ReadEnumOR()
        {
            if (Lexer.Termination())
            {
                if (ReadORDelimit)
                    throw new LexicalException("Invalid enum OR args.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
                Lexer.ReadEnumOR = false;
                ReadORVal = false;
                return ControlFlow.Release;
            }
            if (Lexer.Character == CallLexer.Whitespace)
            {
                Lexer.Skipping = true;
            }
            else if (Lexer.Character == EnumOR)
            {
                if (ReadORDelimit)
                    throw new LexicalException("Invalid enum OR args.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
                ReadORDelimit = true;
                ReadORVal = false;
            }
            else if (ReadORDelimit && !ReadORVal)
            {
                Lexer.ArbitraryReader.ValidIdentifierFirstCharOrThrow();
                ReadORVal = true;
            }
            else if (ReadORVal)
            {
                if (Lexer.Skipping && !ReadORDelimit)
                    throw new LexicalException("Invalid enum OR args.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
                Lexer.ValidIdentifier();
                ReadORDelimit = false;
                Lexer.Skipping = false;
            }
            return ControlFlow.Append;
        }
        internal ControlFlow ReadChar()
        {
            if (!ReadCharValue)
            {
                ReadCharValue = true;
                return ControlFlow.Append;
            }
            else if (!FinishedReadChar)
            {
                if (Lexer.Character != CharDeclr)
                    throw new LexicalException("Characer declarations must be enclosed with apostrophes.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
                FinishedReadChar = true;
                return ControlFlow.Append;
            }
            else if (Lexer.Character == CallLexer.Whitespace)
            {
                Lexer.CrossWhitespace();
                Lexer.ReadChar = false;
                FinishedReadChar = false;
                ReadCharValue = false;
            }
            if (FinishedReadChar)
            {
                FinishedReadChar = false;
                ReadCharValue = false;
                Lexer.ReadChar = false;
            }
            return ControlFlow.Release;
        }
        internal ControlFlow ReadNum()
        {
            if (char.IsDigit(Lexer.Character))
                return ControlFlow.Append;
            if (Lexer.Character == CallLexer.Whitespace)
            {
                Lexer.CrossWhitespace();
                Lexer.ReadDigit = false;
                ReadFloat = false;
                return ControlFlow.Release;
            }
            else
            {
                if (Lexer.Character == MemberAccessOrDecimal)
                {
                    if (ReadFloat)
                        throw new LexicalException("Floats cannot contain multiple decimals.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
                    ReadFloat = true;
                    return ControlFlow.Append;
                }
                if (Lexer.Termination())
                {
                    Lexer.ReadDigit = false;
                    ReadFloat = false;
                    return ControlFlow.Release;
                }
                throw new LexicalException("Numbers can only contain a sign, digits and one decimal.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
            }
        }

        internal ControlFlow ReadString()
        {
            if (StringEnding)
            {
                if (Lexer.Character == CallLexer.Whitespace)
                    Lexer.CrossWhitespace();
                StringEnding = false;
                Lexer.ReadString = false;
                NoEscape = false;
                Escaping = false;
                ReadNoEscDeclr = false;
                JustEscaped = false;
                return ControlFlow.Release;
            }
            if (Escaping)
            {
                Escaping = false;
                JustEscaped = true;
            }
            if (!NoEscape && Lexer.Character == EscSeq)
            {
                if (!JustEscaped)
                    Escaping = true;
                else
                    JustEscaped = false;
            }
            if (Lexer.Character == StringDeclr)
            {
                if (!ReadNoEscDeclr)
                {
                    if (!JustEscaped)
                        StringEnding = true;
                    else
                        JustEscaped = false;
                }
                else
                    ReadNoEscDeclr = false;
            }
            return ControlFlow.Append;
        }
    }
}
