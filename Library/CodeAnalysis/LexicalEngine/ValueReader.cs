using XQuinn.CodeAnalysis;
using static XQuinn.CodeAnalysis.CallLexer;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis.AST;

namespace XQuinn.CodeAnalysis.LexicalEngine
{
    sealed class ValueReader : LexicalObject
    {


        public bool NoEscape { set => _noEscape = value; }

        public bool ReadNoEscDeclr { set => _readNoEscDeclr = value; }

        public bool ReadORVal { set => _readORVal = value; }

        public bool ReadORDelimit { set => _readORDelimit = value; }

        bool _readFloat;
        bool _finishedReadChar;  //Finishers/Enders are primarily for catching trailing garbage data or Lexer.skipping whtiespace - ex Method("hello"  , 22, 33 s)
        bool _readCharValue;
        bool _stringEnding;         //the trailing whitespace after "hello" willbe skipped, and the trailing s after 33 will cause an exception
        bool _noEscape;
        bool _readNoEscDeclr;
        bool _escaping;
        bool _justEscaped;
        bool _readORDelimit;
        bool _readORVal;

        internal void Clear()
        {
            _readFloat = false;
            _finishedReadChar = false;
            _readCharValue = false;
            _stringEnding = false;
            _noEscape = false;
            _readNoEscDeclr = false;
            _escaping = false;
            _justEscaped = false;
            _readORDelimit = false;
            _readORVal = false;
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

        internal bool ReadEnumOR(string invocation)
        {
            if (Termination(Lexer.CurrentValue))
            {
                if (_readORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, Lexer.CurrentValue, Lexer.Writer);
                Lexer.ReadingEnumOR = false;
                _readORVal = false;
                return false;

            }
            if (Lexer.CurrentValue == Whitespace)
            {
                Lexer.Skipping = true;
            }
            else if (Lexer.CurrentValue == EnumOR)
            {
                if (_readORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, Lexer.CurrentValue, Lexer.Writer);
                _readORDelimit = true;
                _readORVal = false;
            }
            else if (_readORDelimit && !_readORVal)
            {
                Lexer.ArbitraryReader.ValidIdentifierFirstCharOrThrow(Lexer.CurrentValue, invocation);
                _readORVal = true;
            }
            else if (_readORVal)
            {
                if (Lexer.Skipping && !_readORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, Lexer.CurrentValue, Lexer.Writer);
                Lexer.ValidIdentifier(Lexer.CurrentValue, invocation);
                _readORDelimit = false;
                Lexer.Skipping = false;
            }
            return true;



        }
        internal bool ReadChar(ref int i, string invocation)
        {
            const string error = "Characer declarations must be enclosed with apostrophes.";
            if (!_readCharValue)
            {
                _readCharValue = true;
                return true;
            }
            else if (!_finishedReadChar)
            {
                if (Lexer.CurrentValue != CharDeclr)
                    throw new LexicalException(error, invocation, Lexer.CurrentValue, Lexer.Writer, i);
                _finishedReadChar = true;
                return true;
            }
            else if (Lexer.CurrentValue == Whitespace)
            {
                SkipWhitespaceTrail(ref i, invocation);
                Lexer.ReadingChar = false;
                _finishedReadChar = false;
                _readCharValue = false;
            }
            if (_finishedReadChar)
            {
                _finishedReadChar = false;
                _readCharValue = false;
                Lexer.ReadingChar = false;
            }
            return false;
        }
        internal bool ReadNum(ref int i, string invocation) //readnum doesnt influence jumps because numeric values are strict and can only contain digits/decimal pointer
        {
            if (Lexer.CurrentValue.IsDigit())
                return true;
            if (Lexer.CurrentValue == Whitespace) //we skip leading and trailing whitespace
            {
                SkipWhitespaceTrail(ref i, invocation);
                Lexer.ReadDigit = false;
                _readFloat = false;
                return false;
            }
            else //nondigit value
            {
                if (Lexer.CurrentValue == MemberAccess)
                {
                    if (_readFloat) throw new LexicalException("Floats cannot contain multiple decimals.", invocation, Lexer.CurrentValue, Lexer.Writer, i);
                    _readFloat = true;
                    return true;
                }
                if (Termination(Lexer.CurrentValue))
                {
                    Lexer.ReadDigit = false;
                    _readFloat = false;
                    return false;
                }
                throw new LexicalException("Numbers can only contain a sign, digits and one decimal.", invocation, Lexer.CurrentValue, Lexer.Writer, i);
            }
        }

        internal bool ReadString(ref int i, string invocation)
        { //here as well, just like in GetContext, there can be some invalid sequences here
            //that will only throw once we reach ValueString (i didnt want to have duplicate checking code in 2 places)
            if (_stringEnding)
            {
                if (Lexer.CurrentValue == Whitespace)
                    SkipWhitespaceTrail(ref i, invocation);
                _stringEnding = false;
                Lexer.ReadingString = false;
                _noEscape = false;
                _escaping = false;
                _readNoEscDeclr = false;
                _justEscaped = false;
                return false; //return false allows parameter control flow to takeover
            }
            if (_escaping)
            {
                _escaping = false;
                _justEscaped = true;
            }
            if (!_noEscape && Lexer.CurrentValue == EscSeq)
            {
                if (!_justEscaped)
                    _escaping = true;
                else
                    _justEscaped = false;
            }
            if (Lexer.CurrentValue == StringDeclr)
            {
                if (!_readNoEscDeclr)
                {
                    if (!_justEscaped)
                        _stringEnding = true;
                    else
                        _justEscaped = false;
                }
                else
                    _readNoEscDeclr = false;
            }
            return true; //skips parameter control flow, appends
        }

    }
}