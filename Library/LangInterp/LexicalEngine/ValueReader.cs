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
            Lexer = lexer;
        }
        internal void ReadParam()
        {
            string prm = Lexer.Writer.ToString();
            ValueString param = new(prm);
            Lexer.Writer.Length = 0;
            Lexer.CurrentMethod!.AddParameter(param);
        }

        void SkipTrailingWhitespace(ref int i, string invocation)
        {
            while (i < invocation.Length)
            {
                i++;
                Lexer.Character = invocation[i];
                if (WhitespaceEnd())
                    return;
                if (Lexer.Character != CallLexer.Whitespace)
                    throw new LexicalException("Detected trailing input after whitespace.", invocation, Lexer.Character, Lexer.Writer, i);
            }
        }

        internal ControlFlow ReadEnumOR(string invocation)
        {
            if (Termination(Lexer.Character))
            {
                if (ReadORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, Lexer.Character, Lexer.Writer);
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
                    throw new LexicalException("Invalid enum OR args.", invocation, Lexer.Character, Lexer.Writer);
                ReadORDelimit = true;
                ReadORVal = false;
            }
            else if (ReadORDelimit && !ReadORVal)
            {
                Lexer.ArbitraryReader.ValidIdentifierFirstCharOrThrow(Lexer.Character, invocation);
                ReadORVal = true;
            }
            else if (ReadORVal)
            {
                if (Lexer.Skipping && !ReadORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, Lexer.Character, Lexer.Writer);
                Lexer.ValidIdentifier(Lexer.Character, invocation);
                ReadORDelimit = false;
                Lexer.Skipping = false;
            }
            return ControlFlow.Append;
        }
        internal ControlFlow ReadChar(ref int i, string invocation)
        {
            if (!ReadCharValue)
            {
                ReadCharValue = true;
                return ControlFlow.Append;
            }
            else if (!FinishedReadChar)
            {
                if (Lexer.Character != CharDeclr)
                    throw new LexicalException("Characer declarations must be enclosed with apostrophes.", invocation, Lexer.Character, Lexer.Writer, i);
                FinishedReadChar = true;
                return ControlFlow.Append;
            }
            else if (Lexer.Character == CallLexer.Whitespace)
            {
                SkipTrailingWhitespace(ref i, invocation);
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
        internal ControlFlow ReadNum(ref int i, string invocation)
        {
            if (char.IsDigit(Lexer.Character))
                return ControlFlow.Append;
            if (Lexer.Character == CallLexer.Whitespace)
            {
                SkipTrailingWhitespace(ref i, invocation);
                Lexer.ReadDigit = false;
                ReadFloat = false;
                return ControlFlow.Release;
            }
            else
            {
                if (Lexer.Character == MemberAccessOrDecimal)
                {
                    if (ReadFloat)
                        throw new LexicalException("Floats cannot contain multiple decimals.", invocation, Lexer.Character, Lexer.Writer, i);
                    ReadFloat = true;
                    return ControlFlow.Append;
                }
                if (Termination(Lexer.Character))
                {
                    Lexer.ReadDigit = false;
                    ReadFloat = false;
                    return ControlFlow.Release;
                }
                throw new LexicalException("Numbers can only contain a sign, digits and one decimal.", invocation, Lexer.Character, Lexer.Writer, i);
            }
        }

        internal ControlFlow ReadString(ref int i, string invocation)
        {
            if (StringEnding)
            {
                if (Lexer.Character == CallLexer.Whitespace)
                    SkipTrailingWhitespace(ref i, invocation);
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
