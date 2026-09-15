using XQuinn.CodeAnalysis;
using XQuinn.CodeAnalysis.AST;
using XQuinn.Extensions;
using System;

using static XQuinn.CodeAnalysis.InvokeLexer;


namespace XQuinn.CodeAnalysis.LexicalEngine
{
    sealed class ArbitraryReader : LexicalObject
    {

        bool _readGeneric { get => _lexer._readGeneric; set => _lexer._readGeneric = value; }

        bool _readFirstCharOfName { get => _lexer._readFirstCharOfName; set => _lexer._readFirstCharOfName = value; }

        bool _readEnum {get=> _lexer._readEnum; set=> _lexer._readEnum = value;}

        bool _readQualifiedMember { set => _lexer._readQualifiedMember = value; }

        bool _readArbitraryLegalValue {set => _lexer._readArbitraryLegalValue = value; }

        bool _methodParamsBegan {set => _lexer._methodParamsBegan = value; }

        bool _terminated { set => _lexer._terminated = value; }

        TypeString? _declaringType => _lexer._declaringType;

        TypeString? _implicit_this => _lexer._implicit_this;

        MethodString? _main { set => _lexer._main = value; }

        MethodString? _currentMethod { get => _lexer._currentMethod; set => _lexer._currentMethod = value; }

        bool _start { set => _lexer._start = value; }

        bool _beganReadingMainMethodName { get => _lexer._beganReadingMainMethodName; set => _lexer._beganReadingMainMethodName = value; }

        int _readingSubparams { get => _lexer._readingSubparams; set => _lexer._readingSubparams = value; }

        int _lastReadingCount { get => _lexer._lastReadingCount; set => _lexer._lastReadingCount = value; }



        public ArbitraryReader(InvokeLexer lexer) : base(lexer)
        {

        }

        internal int ReadArbitrary(ref int i, string invocation)
        {
            if (_value == Whitespace)
                SkipWhitespaceTrail(ref i, invocation);
            if (_value == '<')
                _readGeneric = true;
            else if (_value == MemberAccess)
            {
                _readGeneric = false;
                _readFirstCharOfName = true;
                _readQualifiedMember = true;
                _readArbitraryLegalValue = false;
                return 1;
            }
            else if (_value == MethodStart)
            {
                _readArbitraryLegalValue = false;
                _readGeneric = false;
                _methodParamsBegan = true;
                ReadMethod();
                return 2; //special case where we need to skip to increment if a method is detected
            }           //otherwise it will throw
            else if (!Termination(_value) && _value != '|')
                _lexer.ValidIdentifier(_value, invocation, i);
            else if (_readGeneric)
                return 1; //true
            return 0; //false
        }

        internal bool ReadIdentifier(ref int i, string invocation) //once an arbitrary is determined to be an identifier, it is read with stricter rules
        {
            if (_value == '<')
                _readGeneric = true;
            if (_value == Whitespace)
                SkipWhitespaceTrail(ref i, invocation);
            if (_value == MemberAccess && !_readFirstCharOfName)
            {
                _readFirstCharOfName = true;
                return true;
            }
            if (!_readGeneric && Termination(_value))
            {
                _terminated = true;
                ReadField();
                return false;
            }
            if (_value == MethodStart)
            {
                _readGeneric = false;
                _methodParamsBegan = true;
                ReadMethod();
                return false;
            }
            if (_readFirstCharOfName)
            {
                _readFirstCharOfName = false;
                ValidIdentifierFirstCharOrThrow(_value, invocation, i);
            }
            else
                _lexer.ValidIdentifier(_value, invocation, i);
            return true;
        }

        //all errors stop the program, but these errors mean you really messed up 
        internal bool ReadMainMethod(ref int i, string invocation)
        {
            if (_beganReadingMainMethodName)
            {
                if (_value == Whitespace)
                    SkipWhitespaceTrail(ref i, invocation);
                if (_value == MethodStart)
                {
                    ReadMain();
                    _start = false;
                    _methodParamsBegan = true;
                    _beganReadingMainMethodName = false;
                    return false;
                }
                _lexer.ValidIdentifier(_value, invocation, i);
                return true;
            }
            else if (_value == Whitespace)
                return false;
            if (ValidIdentifierFirstCharOrThrow(_value, invocation, i))
                _beganReadingMainMethodName = true;
            return true;
        }

        void ReadMain()
        {
            //reads everything prior to (
            MethodString method = MethodString.New(_sb.ToString(), null, _declaringType!);// ?? throw new InvalidOperationException());
            _sb.Length = 0;
            _currentMethod = method;
            _main = method;

        }


        void ReadField()
        {
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
            member = lexOutput;
            return null;
        }

        bool ValidIdentifierFirstCharOrThrow(char next, string invocation, int? i)
        {
            const string error = "Identifier names must start with a letter, @ or an underscore.";
            if (!ValidIdentifierFirstChar(next))
                throw i == null ? new LexicalException(error, invocation, next, _sb) : new LexicalException(error, invocation, next, _sb, i.Value);
            return true;
        }

    }
}