using XQuinn.CodeAnalysis;
using static XQuinn.CodeAnalysis.InvokeLexer;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis.AST;

namespace XQuinn.CodeAnalysis.LexicalEngine
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

        bool _escaping { get => _lexer._escaping; set => _lexer._escaping = value; }

        bool _readNoEscDeclr { get => _lexer._readNoEscDeclr; set => _lexer._readNoEscDeclr = value; }

        bool _justEscaped { get => _lexer._justEscaped; set => _lexer._justEscaped = value; }

        bool _readEnumOR { set => _lexer._readEnumOR = value; }

        bool _readORDelimit { get => _lexer._readORDelimit; set => _lexer._readORDelimit = value; }

        bool _readORVal { get => _lexer._readORVal; set => _lexer._readORVal = value; }


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

        internal bool ReadEnumOR(string invocation)
        {
            if (Termination(_value))
            {
                if (_readORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, _value, _sb);
                _readEnumOR = false;
                _readORVal = false;
                return false;

            }
            if (_value != Whitespace) //nonsmart whitespace skip. doesnt throw early, but also doesnt concatenate Pu blic into Public, so its fine
                if (_value == EnumOR)   //this is the one ruleset that actually *does* append all whitespace, this is the only ruleset
                {                           //that allows whitespace inbetween values because it wont mess up parsing later. all other reads expect trailing whitespace to end in a termination.
                    if (_readORDelimit)
                        throw new LexicalException("Invalid enum OR args.", invocation, _value, _sb);
                    _readORDelimit = true;
                    _readORVal = false;
                }
                else if (_readORDelimit)
                {
                    _lexer._arbitraryReader.ValidIdentifierFirstCharOrThrow(_value, invocation);
                    _readORDelimit = false;
                    _readORVal = true;
                }
                else if (_readORVal)
                    _lexer.ValidIdentifier(_value, invocation);
            return true;



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
        { //here as well, just like in GetContext, there can be some invalid sequences here
            //that will only throw once we reach ValueString (i didnt want to have duplicate checking code in 2 places)
            if (_stringEnding)
            {
                if (_value == Whitespace)
                    SkipWhitespaceTrail(ref i, invocation);
                _stringEnding = false;
                _readString = false;
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
            if (!_noEscape && _value == EscSeq)
            {
                if (!_justEscaped)
                    _escaping = true;
                else
                    _justEscaped = false;
            }
            if (_value == StringDeclr)
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