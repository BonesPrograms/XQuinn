using XQuinn.CodeAnalysis;
using XQuinn.CodeAnalysis.AST;
using XQuinn.Extensions;
using System;

using static XQuinn.CodeAnalysis.CallLexer;
using XQuinn.Runtime.NavigatorEngine;


namespace XQuinn.CodeAnalysis.LexicalEngine
{
    sealed class ArbitraryReader : LexicalObject
    {
        internal bool _beganReadingMainMethodName;

        bool _memberAccessing;

        bool _genericParamTerminate;
        bool _readFirstGeneric;
        bool _readFirstCharOfName; //Because it cannot be a digit
        bool _findGenericEnd;
        int _genericEnd;

        int _generic_sub_params;

        int _last_sub_count;
        public void Clear()
        {
            _memberAccessing = false;
            _beganReadingMainMethodName = false;
            _genericParamTerminate = false;
            _readFirstGeneric = false;
            _readFirstCharOfName = false;
            _findGenericEnd = false;
            _genericEnd = -1;
            _generic_sub_params = 0;
            _last_sub_count = 0;
        }






        public ArbitraryReader(CallLexer lexer) : base(lexer)
        {

        }

        internal int ReadArbitrary(ref int i, string invocation)
        {
            if (_lexer._curr_char == Whitespace)
                while (i < invocation.Length)
                {
                    i++;
                    _lexer._curr_char = invocation[i];
                    if (_lexer._curr_char == MemberAccess || WhitespaceEnd())
                        break;
                    if (_lexer._curr_char == EnumOR)
                    {
                        ReadingEnumOR(invocation);
                        return 1;
                    }
                    else if (_lexer._curr_char == GenericDeclr)
                    {
                        return ReadGeneric(invocation, 1);
                    }
                    if (_lexer._curr_char != CallLexer.Whitespace)
                        throw new LexicalException("Detected trailing input after whitespace.", invocation, _lexer._curr_char, _lexer._writer, i);
                }
            if (_lexer._curr_char == EnumOR)
            {
                ReadingEnumOR(invocation);
                return 1;
            }
            else if (_lexer._curr_char == GenericDeclr)
            {
                return ReadGeneric(invocation, 1);
            }
            else if (_lexer._curr_char == MemberAccess)
            {
                _lexer._read_generic_args = false;
                _readFirstCharOfName = true;
                _lexer._read_qualified_member = true;
                _memberAccessing = true;
                _lexer._read_arbitrary_legal_value = false;
                return 1;
            }
            else if (_lexer._curr_char == MethodDeclr)
            {
                _lexer._read_arbitrary_legal_value = false;
                _lexer._read_generic_args = false;
                _lexer._method_params_began = true;
                ReadMethod(invocation);
                return 2; //special case where we need to skip to increment if a method is detected
            }           //otherwise it will throw
            else if (!Termination(_lexer._curr_char))
                _lexer.ValidIdentifier(_lexer._curr_char, invocation);
            else if (_lexer._read_generic_args)
                return 1; //true
            return 0; //false
        }
        void ReadingEnumOR(string invocation)
        {
            if (_lexer._read_generic_args)
                throw new LexicalException("Invalid EnumOR or generic", invocation, _lexer._curr_char, _lexer._writer);
            _lexer._read_arbitrary_legal_value = false;
            _lexer._read_enumOR = true;
            _lexer._value_reader._readORVal = true;
            _lexer._value_reader._readORDelimit = true;
        }

        T? ReadGeneric<T>(string invocation, T? obj)
        {
            CheckGenericBeforeReadGeneric(invocation);
            _findGenericEnd = true;
            _readFirstGeneric = true;
            return obj;
        }

        internal int ReadIdentifier(ref int i, string invocation) //once an arbitrary is determined to be an identifier, it is read with stricter rules
        {
            if (_lexer._curr_char == Whitespace)
                while (i < invocation.Length)
                {
                    i++;
                    _lexer._curr_char = invocation[i];
                    if ((_memberAccessing && ValidIdentifierFirstChar(_lexer._curr_char)) || WhitespaceEnd())
                        break;
                    else if (_lexer._curr_char == GenericDeclr || _lexer._curr_char == MemberAccess)
                        break;
                    else if (_lexer._curr_char != CallLexer.Whitespace)
                        throw new LexicalException("Detected trailing input after whitespace.", invocation, _lexer._curr_char, _lexer._writer, i);
                }
            if (_lexer._curr_char == GenericDeclr)
            {
                return ReadGeneric(invocation, 1);
            }
            if (_lexer._curr_char == MemberAccess && !_readFirstCharOfName)
            {
                _readFirstCharOfName = true;
                _lexer._read_generic_args = false;
                _memberAccessing = true;
                return 1;
            }
            if (!_lexer._read_generic_args && Termination(_lexer._curr_char))
            {
                _lexer._terminated = true;
                _memberAccessing = false;
                ReadField(invocation);
                return 2;
            }
            if (_lexer._curr_char == MethodDeclr)
            {
                _lexer._read_generic_args = false;
                _lexer._method_params_began = true;
                _memberAccessing = false;
                ReadMethod(invocation);
                return 0;
            }
            if (_readFirstCharOfName)
            {
                _memberAccessing = false;
                _readFirstCharOfName = false;
                ValidIdentifierFirstCharOrThrow(_lexer._curr_char, invocation);
            }
            else
                _lexer.ValidIdentifier(_lexer._curr_char, invocation);
            return 1;
        }


        internal bool ReadGeneric(ref int i, string invocation)
        {

            if (_lexer._curr_char  == GenericDeclr) //generics can only be Lexer.terminated by these, so if its not, the lex will throw at the end
            {
                if (_last_sub_count > _generic_sub_params)
                    _last_sub_count--;
                if (_last_sub_count == 0)
                {
                    _readFirstGeneric = false;
                    _lexer._skipping = false;
                    _lexer._read_generic_args = false;
                    _memberAccessing = false;
                }
                if (_generic_sub_params > 0)
                    _generic_sub_params--;
            }
            else if (_lexer._curr_char == MemberAccess)
                _memberAccessing = true;
            else if (_lexer._curr_char == ParamTerminate) //wnt to add member access checks 2 this
            {
                if (_genericParamTerminate)
                    throw new LexicalException("Invalid generic arguments.", invocation, _lexer._curr_char, _lexer._writer);
                _genericParamTerminate = true;
                _lexer._skipping = false;
            }
            else if (_lexer._curr_char == Whitespace)
            {
                _lexer._skipping = true; //unlike most other reads, this one allows whitespace, so instead of Lexer.skipping to Termination and ending the read
                return false; //we must instead increment one by one and keep the read active until we reach a proper termination
            }
            else if (_lexer._curr_char != GenericDeclr && _lexer._curr_char != GenericTerminate)
            {
                if (_lexer._skipping && !_readFirstGeneric && !_genericParamTerminate && !_memberAccessing) //this specifically checks if the last value was a char
                    throw new LexicalException("Invalid generic arguments.", invocation, _lexer._curr_char, _lexer._writer); //in essence this means youre putting something like "String"
                if (_memberAccessing || (_genericParamTerminate && !_readFirstGeneric))
                    ValidIdentifierFirstCharOrThrow(_lexer._curr_char, invocation);
                else
                    _lexer.ValidIdentifier(_lexer._curr_char, invocation);
                _genericParamTerminate = false;
                _readFirstGeneric = false;
                _lexer._skipping = false;
                _memberAccessing = false;
            }
            else if (_lexer._curr_char == GenericDeclr)
            {
                if (_readFirstGeneric)
                    throw new LexicalException("Invalid generic arguments.", invocation, _lexer._curr_char, _lexer._writer);
                _readFirstGeneric = true;
                _generic_sub_params++;
                _last_sub_count++;
            }
            else if (_lexer._curr_char == GenericTerminate && _readFirstGeneric)
                throw new LexicalException("Invalid generic arguments.", invocation, _lexer._curr_char, _lexer._writer);
            return true;

        }

        //all errors stop the program, but these errors mean you really messed up 
        internal bool ReadMainMethod(ref int i, string invocation)
        {
            if (_beganReadingMainMethodName)
            {
                if (_lexer._read_generic_args)
                {
                   return ReadGeneric(ref i, invocation); //readgeneric is so special isnt it, this is the only read called by another read, but it works
                }
                if (_lexer._curr_char == Whitespace)
                    while (i < invocation.Length)
                    {
                        i++;
                        _lexer._curr_char = invocation[i];
                        if (ValidIdentifierFirstChar(_lexer._curr_char) || WhitespaceEnd() || _lexer._curr_char == GenericTerminate)
                        {
                            if (_lexer._curr_char == MethodDeclr)  //this allows Method ( ) for the first/main method
                                break; //generics handle it on their own, but i need a specail case
                        }
                        else if (_lexer._curr_char == GenericDeclr)
                            break;
                        else if (_lexer._curr_char != CallLexer.Whitespace)
                            throw new LexicalException("Detected trailing input after whitespace.", invocation, _lexer._curr_char, _lexer._writer, i);
                    }
                if (_lexer._curr_char == MethodDeclr)
                {
                    ReadMain();
                    _lexer._read_generic_args = false;
                    _lexer._start = false;
                    _lexer._method_params_began = true;
                    _beganReadingMainMethodName = false;
                    return false;
                }
                if (_lexer._curr_char == GenericDeclr)
                    ReadGeneric<object>(invocation, null);
                else
                    _lexer.ValidIdentifier(_lexer._curr_char, invocation);
                return true;
            }
            else if (_lexer._curr_char == Whitespace)
                return false;
            if (ValidIdentifierFirstCharOrThrow(_lexer._curr_char, invocation))
                _beganReadingMainMethodName = true;
            return true;
        }


        void ReadMain()
        {
            MethodString method = MethodString.New(_lexer._writer.ToString(), null, _lexer._declr_type!);// ?? throw new InvalidOperationException());
            _lexer._writer.Length = 0;
            _lexer._curr_method = method;
            _lexer._main = method;

        }


        void ReadField(string invocation)
        {
            if (_readFirstCharOfName) //i expect ethod( class<int>. , ) readqualifiedmember to be true here but it is not -ah yes thats cause , causes it to be read as a field
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, _lexer._writer);
            string typename = ResolveMemberAccess(out string fieldname)!;
            TypeString type = ImplicitDeclaredOrNew(typename);
            FieldString field = new(fieldname, type);
            _lexer._writer.Length = 0;
            _lexer._curr_method!.AddParameter(field);
            _lexer._read_qualified_member = false;
        }


        void ReadMethod(string invocation)
        {
            if (_readFirstCharOfName) //i expect ethod( class<int>. , ) readqualifiedmember to be true here but it is not -ah yes thats cause , causes it to be read as a field
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, _lexer._writer);
            string? typename = ResolveMemberAccess(out string methodname);
            TypeString type = ImplicitDeclaredOrNew(typename);
            MethodString method = MethodString.New(methodname, _lexer._curr_method, type);
            _lexer._writer.Length = 0;
            _lexer._curr_method!.AddParameter(method);
            _lexer._curr_method = method;
            _lexer._read_qualified_member = false;
            _lexer._reading_sub_params++;
            _lexer._last_nested_read_count++;
        }

        TypeString ImplicitDeclaredOrNew(string? name)
        {
            if (name == null)
                return _lexer._implicit_this ?? throw new InvalidOperationException("Cannot use implicit this, no implicit this has been provided.");
            if (name.EqualsCaseless(_lexer._implicit_this?.StringID)) // == this or == _key
                return _lexer._implicit_this!;
            if (name.EqualsCaseless(_lexer._declr_type!.StringID))
                return _lexer._declr_type;
            return TypeString.New(name);
        }
        string? ResolveMemberAccess(out string member) //returns typename, outputs the accessed member
        {
            string qualifiedMember = _lexer._writer.ToString();
            _lexer._writer.Length = 0;
            return MiniLexer.ResolveMemberAccess(qualifiedMember, out member, out _);
        }

        void CheckGenericBeforeReadGeneric(string invocation)
        {
            if (_lexer._read_generic_args)
                throw new LexicalException("Invalid generic arguments.", invocation, _lexer._writer);
            _lexer._read_generic_args = true;
        }

        internal bool ValidIdentifierFirstCharOrThrow(char next, string invocation)
        {
            const string error = "Identifier names must Lexer.start with a letter or an underscore.";
            if (!ValidIdentifierFirstChar(next))
                throw new LexicalException(error, invocation, next, _lexer._writer);
            return true;
        }

    }
}