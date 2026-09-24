using XQuinn.CodeAnalysis;
using static XQuinn.CodeAnalysis.CallLexer;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis.AST;

namespace XQuinn.CodeAnalysis.LexicalEngine
{
    sealed class ValueReader : LexicalObject
    {


        internal bool _noEscape;
        internal bool _readNoEscDeclr;
        internal bool _readORDelimit;
        internal bool _readORVal;

        bool _readFloat;
        bool _finishedReadChar;  //Finishers/Enders are primarily for catching trailing garbage data or Lexer.skipping whtiespace - ex Method("hello"  , 22, 33 s)
        bool _readCharValue;
        bool _stringEnding;         //the trailing whitespace after "hello" willbe skipped, and the trailing s after 33 will cause an exception

        bool _escaping;
        bool _justEscaped;


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
            _lexer = lexer;
        }
        internal void ReadParam()
        {
            string prm = _lexer._writer.ToString();
            ValueString param = new(prm);
            _lexer._writer.Length = 0;
            _lexer._curr_method!.AddParameter(param);
        }

        internal bool ReadEnumOR(string invocation)
        {
            if (Termination(_lexer._curr_char))
            {
                if (_readORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, _lexer._curr_char, _lexer._writer);
                _lexer._read_enumOR = false;
                _readORVal = false;
                return false;

            }
            if (_lexer._curr_char == Whitespace)
            {
                _lexer._skipping = true;
            }
            else if (_lexer._curr_char == EnumOR)
            {
                if (_readORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, _lexer._curr_char, _lexer._writer);
                _readORDelimit = true;
                _readORVal = false;
            }
            else if (_readORDelimit && !_readORVal)
            {
                _lexer._arbitrary_reader.ValidIdentifierFirstCharOrThrow(_lexer._curr_char, invocation);
                _readORVal = true;
            }
            else if (_readORVal)
            {
                if (_lexer._skipping && !_readORDelimit)
                    throw new LexicalException("Invalid enum OR args.", invocation, _lexer._curr_char, _lexer._writer);
                _lexer.ValidIdentifier(_lexer._curr_char, invocation);
                _readORDelimit = false;
                _lexer._skipping = false;
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
                if (_lexer._curr_char != CharDeclr)
                    throw new LexicalException(error, invocation, _lexer._curr_char, _lexer._writer, i);
                _finishedReadChar = true;
                return true;
            }
            else if (_lexer._curr_char == Whitespace)
            {
                SkipWhitespaceTrail(ref i, invocation);
                _lexer._reading_char = false;
                _finishedReadChar = false;
                _readCharValue = false;
            }
            if (_finishedReadChar)
            {
                _finishedReadChar = false;
                _readCharValue = false;
                _lexer._reading_char = false;
            }
            return false;
        }
        internal bool ReadNum(ref int i, string invocation) //readnum doesnt influence jumps because numeric values are strict and can only contain digits/decimal pointer
        {
            if (_lexer._curr_char.IsDigit())
                return true;
            if (_lexer._curr_char == Whitespace) //we skip leading and trailing whitespace
            {
                SkipWhitespaceTrail(ref i, invocation);
                _lexer._read_digit = false;
                _readFloat = false;
                return false;
            }
            else //nondigit value
            {
                if (_lexer._curr_char == MemberAccess)
                {
                    if (_readFloat) throw new LexicalException("Floats cannot contain multiple decimals.", invocation, _lexer._curr_char, _lexer._writer, i);
                    _readFloat = true;
                    return true;
                }
                if (Termination(_lexer._curr_char))
                {
                    _lexer._read_digit = false;
                    _readFloat = false;
                    return false;
                }
                throw new LexicalException("Numbers can only contain a sign, digits and one decimal.", invocation, _lexer._curr_char, _lexer._writer, i);
            }
        }

        internal bool ReadString(ref int i, string invocation)
        { //here as well, just like in GetContext, there can be some invalid sequences here
            //that will only throw once we reach ValueString (i didnt want to have duplicate checking code in 2 places)
            if (_stringEnding)
            {
                if (_lexer._curr_char == Whitespace)
                    SkipWhitespaceTrail(ref i, invocation);
                _stringEnding = false;
                _lexer._read_string = false;
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
            if (!_noEscape && _lexer._curr_char == EscSeq)
            {
                if (!_justEscaped)
                    _escaping = true;
                else
                    _justEscaped = false;
            }
            if (_lexer._curr_char == StringDeclr)
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