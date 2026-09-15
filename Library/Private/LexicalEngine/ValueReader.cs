using XQuinn.CodeAnalysis;
using static XQuinn.CodeAnalysis.InvokeLexer;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis.AST;

namespace XQuinn.Private.LexicalEngine
{
    sealed class ValueReader : LexicalObject
    {

        bool _readDigit { set => _lexer._readDigit = value; }

        bool _readFloat { get => _lexer._readFloat; set => _lexer._readFloat = value; }

        bool _stringEnding { get => _lexer._stringEnding; set => _lexer._stringEnding = value; }

        bool _readString { set => _lexer._readString = value; }

        bool _noEscape { get => _lexer._noEscape; set => _lexer._noEscape = value; }

        bool _readCharValue { get => _lexer._readCharValue; set => _lexer._readCharValue = value; }

        bool _finishedReadChar { get => _lexer._finishedReadChar; set => _lexer._finishedReadChar = value; }

        bool _readChar { set => _lexer._readChar = value; }

        public ValueReader(InvokeLexer lexer) : base(lexer)
        {
            _lexer = lexer;
        }
        internal void ReadParam()
        {
            string prm = _sb.ToString();
            ValueString param = new(prm);
            _sb.Length = 0;
            _lexer._currentMethod!.AddParameter(param);
        }
        internal bool ReadChar(ref int i, string invocation)
        {
            const string error = "Characer declarations must be enclosed with character declaration communicators (apostrophes).";
            if (!_readCharValue)
            {
                _readCharValue = true;
                return true;
            }
            else if (!_finishedReadChar)
            {
                if (_value != CharDeclr)
                    throw new LexicalException(error, invocation, _value, _sb, i);
                _finishedReadChar = true;
                return true;
            }
            else if (_value == Whitespace)
            {
                SkipWhitespaceTrail(ref i, invocation);
                _readChar = false;
                _finishedReadChar = false;
                _readCharValue = false;
            }
            if (_finishedReadChar)
            {
                _finishedReadChar = false;
                _readCharValue = false;
                _readChar = false;
            }
            return false;
        }
        internal bool ReadNum(ref int i, string invocation) //readnum doesnt influence jumps because numeric values are strict and can only contain digits/decimal pointer
        {
            if (_value.IsDigit())
                return true;
            if (_value == Whitespace) //we skip leading and trailing whitespace
            {
                SkipWhitespaceTrail(ref i, invocation);
                _readDigit = false;
                _readFloat = false;
                return false;
            }
            else //nondigit value
            {
                if (_value == MemberAccess)
                {
                    if (_readFloat) throw new LexicalException("Floats cannot contain multiple periods.", invocation, _value, _sb, i);
                    _readFloat = true;
                    return true;
                }
                if (Termination(_value))
                {
                    _readDigit = false;
                    _readFloat = false;
                    return false;
                }
                throw new LexicalException("Numbers can only contain digits or one decimal.", invocation, _value, _sb, i);
            }
        }

        internal bool ReadString(ref int i, string invocation)
        {

            if (_stringEnding)
            {
                if (_value == Whitespace)
                    SkipWhitespaceTrail(ref i, invocation);
                _stringEnding = false;
                _readString = false;
                _noEscape = false;
                return false; //return false allows parameter control flow to takeover
            }
            if (!_noEscape && _value == EscSeq)
            {
                i++;
                _value = invocation[i];
                if (_value == '"')
                    _sb.Append(_value); //i actually have a rule against doing direct appends but
                i++;                             //im feeling lazy right now and it works really smooth here (usually doing direct appends is a bad idea)
                _value = invocation[i];             //(youre usually just supposed to return true or false which may jump to append in the main loop)
            }
            if (_value == StringDeclr)
            {
                _stringEnding = true;
            }
            return true; //skips parameter control flow, appends
        }
        // static bool IsCommunicator(char val) => val switch
        // {
        //     MethodStart or MethodTerminate or ParamTerminate or MemberAccess or StringDeclr or EscSeq or CharDeclr => true,
        //     _=>false
        // };


    }
}