using XQuinn.CodeAnalysis;
using XQuinn.CodeAnalysis.AST;
using XQuinn.Extensions;
using System;

using static XQuinn.CodeAnalysis.CallLexer;


namespace XQuinn.CodeAnalysis.LexicalEngine
{
    sealed class ArbitraryReader : LexicalObject
    {

        bool _skipping { get => _lexer._skipping; set => _lexer._skipping = value; }
        bool _readGeneric { get => _lexer._readGeneric; set => _lexer._readGeneric = value; }

        bool _readFirstGeneric { get => _lexer._readFirstGeneric; set => _lexer._readFirstGeneric = value; }

        bool _genericParamTerminate { get => _lexer._genericParamTerminate; set => _lexer._genericParamTerminate = value; }


        bool _readFirstCharOfName { get => _lexer._readFirstCharOfName; set => _lexer._readFirstCharOfName = value; }
        bool _readQualifiedMember { set => _lexer._readQualifiedMember = value; }

        bool _readArbitraryLegalValue { set => _lexer._readArbitraryLegalValue = value; }

        bool _methodParamsBegan { set => _lexer._methodParamsBegan = value; }

        bool _terminated { set => _lexer._terminated = value; }

        bool _readEnumOR { set => _lexer._readEnumOR = value; }

        bool _readORVal { set => _lexer._readORVal = value; }


        TypeString? _declaringType => _lexer._declaringType;

        TypeString? _implicit_this => _lexer._implicit_this;

        MethodString? _main { set => _lexer._main = value; }

        MethodString? _currentMethod { get => _lexer._currentMethod; set => _lexer._currentMethod = value; }

        bool _start { set => _lexer._start = value; }
        bool _readORDelimit { set => _lexer._readORDelimit = value; }
        bool _beganReadingMainMethodName { get => _lexer._beganReadingMainMethodName; set => _lexer._beganReadingMainMethodName = value; }

        bool _memberAccessing { get => _lexer._memberAccessing; set => _lexer._memberAccessing = value; }

        int _readingSubparams { get => _lexer._readingSubparams; set => _lexer._readingSubparams = value; }

        int _lastReadingCount { get => _lexer._lastReadingCount; set => _lexer._lastReadingCount = value; }





        public ArbitraryReader(CallLexer lexer) : base(lexer)
        {

        }

        int ArbitraryGeneric(string invocation)
        {
            CheckGenericBeforeReadGeneric(invocation);
            _readFirstGeneric = true;
            return 1;
        }
        internal int ReadArbitrary(ref int i, string invocation)
        {
            if (_value == Whitespace)
                while (i < invocation.Length)
                {
                    i++;
                    _value = invocation[i];
                    if (_value == MemberAccess || WhitespaceEnd())
                        break;
                    if (_value == EnumOR)
                    {
                        ReadingEnumOR(invocation);
                        return 1;
                    }
                    else if (_value == GenericDeclr)
                    {
                        return ArbitraryGeneric(invocation);
                    }
                    if (_value != CallLexer.Whitespace)
                        throw new LexicalException("Detected trailing input after whitespace.", invocation, _value, _sb, i);
                }
            if (_value == EnumOR)
            {
                ReadingEnumOR(invocation);
                return 1;
            }
            else if (_value == GenericDeclr)
            {
                return ArbitraryGeneric(invocation);
            }
            else if (_value == MemberAccess)
            {
                _readGeneric = false;
                _readFirstCharOfName = true;
                _readQualifiedMember = true;
                _memberAccessing = true;
                _readArbitraryLegalValue = false;
                return 1;
            }
            else if (_value == MethodDeclr)
            {
                _readArbitraryLegalValue = false;
                _readGeneric = false;
                _methodParamsBegan = true;
                ReadMethod();
                return 2; //special case where we need to skip to increment if a method is detected
            }           //otherwise it will throw
            else if (!Termination(_value))
                _lexer.ValidIdentifier(_value, invocation);
            else if (_readGeneric)
                return 1; //true
            return 0; //false
        }
        void ReadingEnumOR(string invocation)
        {
            if (_readGeneric)
                throw new LexicalException("Invalid EnumOR or generic", invocation, _value, _sb);
            _readArbitraryLegalValue = false;
            _readEnumOR = true;
            _readORVal = true;
            _readORDelimit = true;
        }

        bool IdentifierGeneric(string invocation)
        {
            CheckGenericBeforeReadGeneric(invocation);
            _readFirstGeneric = true;
            return true;
        }
        internal bool ReadIdentifier(ref int i, string invocation) //once an arbitrary is determined to be an identifier, it is read with stricter rules
        {
            if (_value == Whitespace)
                while (i < invocation.Length)
                {
                    i++;
                    _value = invocation[i];
                    if ((_memberAccessing && ValidIdentifierFirstChar(_value)) || WhitespaceEnd())
                        break;
                    else if (_value == GenericDeclr)
                    {
                        return IdentifierGeneric(invocation);
                    }
                    else if (_value != CallLexer.Whitespace)
                        throw new LexicalException("Detected trailing input after whitespace.", invocation, _value, _sb, i);
                }
            if (_value == GenericDeclr)
            {
                return IdentifierGeneric(invocation);
            }
            if (_value == MemberAccess && !_readFirstCharOfName)
            {
                _readFirstCharOfName = true;
                _readGeneric = false;
                _memberAccessing = true;
                return true;
            }
            if (!_readGeneric && Termination(_value))
            {
                _terminated = true;
                _memberAccessing = false;
                ReadField(invocation);
                return false;
            }
            if (_value == MethodDeclr)
            {
                _readGeneric = false;
                _methodParamsBegan = true;
                _memberAccessing = false;
                ReadMethod();
                return false;
            }
            if (_readFirstCharOfName)
            {
                _memberAccessing = false;
                _readFirstCharOfName = false;
                ValidIdentifierFirstCharOrThrow(_value, invocation);
            }
            else
                _lexer.ValidIdentifier(_value, invocation);
            return true;
        }

        internal bool ReadGeneric(ref int i, string invocation)
        {
            if (_value == ParamTerminate)
            {
                if (_genericParamTerminate)
                    throw new LexicalException("Invalid generic arguments.", invocation, _value, _sb);
                _genericParamTerminate = true;
                _skipping = false;
            }
            else if (_value == Whitespace)
            {
                _skipping = true; //unlike most other reads, this one allows whitespace, so instead of skipping to Termination and ending the read
                return false; //we must instead increment one by one and keep the read active until we reach a proper termination
            }
            else if (_value == MemberAccess || _value == MethodDeclr) //generics can only be terminated by these, so if its not, the lex will throw at the end
            {
                if (_readFirstGeneric) //ex. class<int.method()
                    throw new LexicalException("Invalid generic arguments.", invocation, _value, _sb); //in essence this means youre putting something like "Stri ng"
                _readFirstGeneric = false;
                _skipping = false;
                i--; //kinda lazy but i did NOT feel like copying a bunch of code
                _readGeneric = false;  //readGeneric can be active during ReadIdentifier ReadArbitrary and ReadMainMethod (its the only read that can be active while another read is active)
                return false; //when its over, it needs to back up, so that way the char is the same when it returns to those methods, because those methods have branches to handle these chars
            }
            else if (_value != GenericDeclr && _value != GenericTerminate)
            {
                if (_skipping && !_readFirstGeneric && !_genericParamTerminate) //this specifically checks if the last value was a char
                    throw new LexicalException("Invalid generic arguments.", invocation, _value, _sb); //in essence this means youre putting something like "Stri ng"
                ValidIdentifierFirstCharOrThrow(_value, invocation);
                _genericParamTerminate = false;
                _readFirstGeneric = false;
                _skipping = false;
            }
            else if (_value == GenericDeclr)
            {
                if (_readFirstGeneric)
                    throw new LexicalException("Invalid generic arguments.", invocation, _value, _sb);
                _readFirstGeneric = true;
            }
            else if (_value == GenericTerminate && _readFirstGeneric)
                throw new LexicalException("Invalid generic arguments.", invocation, _value, _sb);
            return true;

        }

        //all errors stop the program, but these errors mean you really messed up 
        internal bool ReadMainMethod(ref int i, string invocation)
        {
            if (_beganReadingMainMethodName)
            {
                if (_readGeneric)
                    ReadGeneric(ref i, invocation); //readgeneric is so special isnt it, this is the only read called by another read, but it works
                else if (_value == Whitespace)
                    while (i < invocation.Length)
                    {
                        i++;
                        _value = invocation[i];
                        if (ValidIdentifierFirstChar(_value) || WhitespaceEnd() || _value == GenericTerminate)
                        {
                            if (_value == MethodDeclr)  //this allows Method ( ) for the first/main method
                                return ReadMainJump();                     //^whitespace
                            break;                              //kinda funky other ones usually just break, but havent come up with a fix rn
                        }                                       //cannot modify this controlflow otherwise itll cause issues if the first method is generic
                        else if (_value == GenericDeclr)
                        {
                            return IdentifierGeneric(invocation);
                        }
                        else if (_value != CallLexer.Whitespace)
                            throw new LexicalException("Detected trailing input after whitespace.", invocation, _value, _sb, i);
                    }
                else if (_value == MethodDeclr)
                {
                    return ReadMainJump();
                }
                else if (_value == GenericDeclr)
                {
                    CheckGenericBeforeReadGeneric(invocation);
                    _readFirstGeneric = true;
                }
                else
                    _lexer.ValidIdentifier(_value, invocation);
                return true;
            }
            else if (_value == Whitespace)
                return false;
            if (ValidIdentifierFirstCharOrThrow(_value, invocation))
                _beganReadingMainMethodName = true;
            return true;
        }

        bool ReadMainJump()
        {
            ReadMain();
            _readGeneric = false;
            _start = false;
            _methodParamsBegan = true;
            _beganReadingMainMethodName = false;
            return false;
        }

        void ReadMain()
        {
            //reads everything prior to (
            MethodString method = MethodString.New(_sb.ToString(), null, _declaringType!);// ?? throw new InvalidOperationException());
            _sb.Length = 0;
            _currentMethod = method;
            _main = method;

        }


        void ReadField(string invocation)
        {
            if (_readFirstCharOfName) //i expect ethod( class<int>. , ) readqualifiedmember to be true here but it is not -ah yes thats cause , causes it to be read as a field
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, _sb);
            string typename = ResolveMemberAccess(out string fieldname)!;
            TypeString type = ImplicitDeclaredOrNew(typename);
            FieldString field = new(fieldname, type);
            _sb.Length = 0;
            _currentMethod!.AddParameter(field);
            _readQualifiedMember = false;
        }


        void ReadMethod()
        {
            string? typename = ResolveMemberAccess(out string methodname);
            TypeString type = ImplicitDeclaredOrNew(typename);
            MethodString method = MethodString.New(methodname, _currentMethod, type);
            _sb.Length = 0;
            _currentMethod!.AddParameter(method);
            _currentMethod = method;
            _readQualifiedMember = false;
            _readingSubparams++;
            _lastReadingCount++;
        }

        TypeString ImplicitDeclaredOrNew(string? name)
        {
            if (name == null)
                return _implicit_this ?? throw new InvalidOperationException("Cannot use implicit this, no implicit this has been provided.");
            if (name.EqualsCaseless(_implicit_this?.StringID)) // == this or == _key
                return _implicit_this!;
            if (name.EqualsCaseless(_declaringType!.StringID))
                return _declaringType;
            return TypeString.New(name);
        }
        string? ResolveMemberAccess(out string member) //returns typename, outputs the accessed member
        {
            string lexOutput = _sb.ToString();
            _sb.Length = 0;
            int? lastAccessorIndex = null;
            for (int i = 1; i < lexOutput.Length; i++)
                if (lexOutput[i] == MemberAccess)
                    lastAccessorIndex = i;
            if (lastAccessorIndex != null)
            {
                member = lexOutput.Substring(lastAccessorIndex.Value + 1);
                return lexOutput.Remove(lastAccessorIndex.Value);
            }
            else
            {
                member = lexOutput;
                return null;
            }
        }

        void CheckGenericBeforeReadGeneric(string invocation)
        {
            if (_readGeneric)
                throw new LexicalException("Invalid generic arguments.", invocation, _sb);
            _readGeneric = true;
        }

        internal bool ValidIdentifierFirstCharOrThrow(char next, string invocation)
        {
            const string error = "Identifier names must start with a letter or an underscore.";
            if (!ValidIdentifierFirstChar(next))
                throw new LexicalException(error, invocation, next, _sb);
            return true;
        }

    }
}