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


        public bool BeganReadingMainMethodName => _beganReadingMainMethodName;

        public void Clear()
        {
            _memberAccessing = false;
            _beganReadingMainMethodName = false;
            _genericParamTerminate = false;
            _readFirstGeneric = false;
            _readFirstCharOfName = false;
            _findGenericEnd = false;
            _genericEnd = -1;
        }
        bool _memberAccessing;
        bool _beganReadingMainMethodName;
        bool _genericParamTerminate;
        bool _readFirstGeneric;
        bool _readFirstCharOfName; //Because it cannot be a digit
        bool _findGenericEnd;
        int _genericEnd;


        int ReadingSubparams { get => Lexer.ReadingSubparams; set => Lexer.ReadingSubparams = value; }

        int LastReadingCount { get => Lexer.LastReadCount; set => Lexer.LastReadCount = value; }





        public ArbitraryReader(CallLexer lexer) : base(lexer)
        {

        }

        internal int ReadArbitrary(ref int i, string invocation)
        {
            if (Lexer.CurrentValue == Whitespace)
                while (i < invocation.Length)
                {
                    i++;
                    Lexer.CurrentValue = invocation[i];
                    if (Lexer.CurrentValue == MemberAccess || WhitespaceEnd())
                        break;
                    if (Lexer.CurrentValue == EnumOR)
                    {
                        ReadingEnumOR(invocation);
                        return 1;
                    }
                    else if (Lexer.CurrentValue == GenericDeclr)
                    {
                        return ReadGeneric(invocation, 1);
                    }
                    if (Lexer.CurrentValue != CallLexer.Whitespace)
                        throw new LexicalException("Detected trailing input after whitespace.", invocation, Lexer.CurrentValue, Lexer.Writer, i);
                }
            if (Lexer.CurrentValue == EnumOR)
            {
                ReadingEnumOR(invocation);
                return 1;
            }
            else if (Lexer.CurrentValue == GenericDeclr)
            {
                return ReadGeneric(invocation, 1);
            }
            else if (Lexer.CurrentValue == MemberAccess)
            {
                Lexer.ReadingGeneric = false;
                _readFirstCharOfName = true;
                Lexer.ReadQualifiedMember = true;
                _memberAccessing = true;
                Lexer.ReadArbitraryLegalValue = false;
                return 1;
            }
            else if (Lexer.CurrentValue == MethodDeclr)
            {
                Lexer.ReadArbitraryLegalValue = false;
                Lexer.ReadingGeneric = false;
                Lexer.MethodParamsBegan = true;
                ReadMethod(invocation);
                return 2; //special case where we need to skip to increment if a method is detected
            }           //otherwise it will throw
            else if (!Termination(Lexer.CurrentValue))
                Lexer.ValidIdentifier(Lexer.CurrentValue, invocation);
            else if (Lexer.ReadingGeneric)
                return 1; //true
            return 0; //false
        }
        void ReadingEnumOR(string invocation)
        {
            if (Lexer.ReadingGeneric)
                throw new LexicalException("Invalid EnumOR or generic", invocation, Lexer.CurrentValue, Lexer.Writer);
            Lexer.ReadArbitraryLegalValue = false;
            Lexer.ReadingEnumOR = true;
            Lexer.ValueReader.ReadORVal = true;
            Lexer.ValueReader.ReadORDelimit = true;
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
            if (Lexer.CurrentValue == Whitespace)
                while (i < invocation.Length)
                {
                    i++;
                    Lexer.CurrentValue = invocation[i];
                    if ((_memberAccessing && ValidIdentifierFirstChar(Lexer.CurrentValue)) || WhitespaceEnd())
                        break;
                    else if (Lexer.CurrentValue == GenericDeclr || Lexer.CurrentValue == MemberAccess) 
                        break;
                    else if (Lexer.CurrentValue != CallLexer.Whitespace)
                        throw new LexicalException("Detected trailing input after whitespace.", invocation, Lexer.CurrentValue, Lexer.Writer, i);
                }
            if (Lexer.CurrentValue == GenericDeclr)
            {
                return ReadGeneric(invocation, 1);
            }
            if (Lexer.CurrentValue == MemberAccess && !_readFirstCharOfName)
            {
                _readFirstCharOfName = true;
                Lexer.ReadingGeneric = false;
                _memberAccessing = true;
                return 1;
            }
            if (!Lexer.ReadingGeneric && Termination(Lexer.CurrentValue))
            {
                Lexer.Terminated = true;
                _memberAccessing = false;
                ReadField(invocation);
                return 2;
            }
            if (Lexer.CurrentValue == MethodDeclr)
            {
                Lexer.ReadingGeneric = false;
                Lexer.MethodParamsBegan = true;
                _memberAccessing = false;
                ReadMethod(invocation);
                return 0;
            }
            if (_readFirstCharOfName)
            {
                _memberAccessing = false;
                _readFirstCharOfName = false;
                ValidIdentifierFirstCharOrThrow(Lexer.CurrentValue, invocation);
            }
            else
                Lexer.ValidIdentifier(Lexer.CurrentValue, invocation);
            return 1;
        }


        internal bool ReadGeneric(ref int i, string invocation)
        {

            if (_findGenericEnd)
            {
                int ending = -1;
                bool passedComma = false;
                for (int x = i; x < invocation.Length; x++)
                {
                    char c = invocation[x];
                    if (passedComma)
                    {
                        if (c == GenericDeclr || c.IsLetter() || c.IsDigit() || c == ValidNonAlphaNumeric)
                            passedComma = false;
                    }
                    if (c == GenericTerminate)
                    {
                        if (passedComma)
                            passedComma = false;
                        ending = x;
                    }
                    else if (c == ParamTerminate)
                    {
                        if (passedComma)
                            break;
                        passedComma = true;
                    }
                    else if (c == MethodDeclr)
                        break;
                }
                if (ending == -1)
                    throw new LexicalException("Invalid generic arguments.", invocation, Lexer.Writer);
                _genericEnd = ending;
                _findGenericEnd = false;
            }
            if (i == _genericEnd) //generics can only be Lexer.terminated by these, so if its not, the lex will throw at the end
            {
                _genericEnd = 0;
                _readFirstGeneric = false;
                Lexer.Skipping = false;
                Lexer.Writer.Append(GenericTerminate);
                Lexer.ReadingGeneric = false;
                _memberAccessing = false;
                return false;
            }
            else if (Lexer.CurrentValue == MemberAccess)
                _memberAccessing = true;
            else if (Lexer.CurrentValue == ParamTerminate) //wnt to add member access checks 2 this
            {
                if (_genericParamTerminate)
                    throw new LexicalException("Invalid generic arguments.", invocation, Lexer.CurrentValue, Lexer.Writer);
                _genericParamTerminate = true;
                Lexer.Skipping = false;
            }
            else if (Lexer.CurrentValue == Whitespace)
            {
                Lexer.Skipping = true; //unlike most other reads, this one allows whitespace, so instead of Lexer.skipping to Termination and ending the read
                return false; //we must instead increment one by one and keep the read active until we reach a proper termination
            }
            else if (Lexer.CurrentValue != GenericDeclr && Lexer.CurrentValue != GenericTerminate)
            {
                if (Lexer.Skipping && !_readFirstGeneric && !_genericParamTerminate && !_memberAccessing) //this specifically checks if the last value was a char
                    throw new LexicalException("Invalid generic arguments.", invocation, Lexer.CurrentValue, Lexer.Writer); //in essence this means youre putting something like "String"
                if (_genericParamTerminate && !_readFirstGeneric)
                    ValidIdentifierFirstCharOrThrow(Lexer.CurrentValue, invocation);
                else if (_memberAccessing)
                    Lexer.ValidIdentifier(Lexer.CurrentValue, invocation);
                _genericParamTerminate = false;
                _readFirstGeneric = false;
                Lexer.Skipping = false;
                _memberAccessing = false;
            }
            else if (Lexer.CurrentValue == GenericDeclr)
            {
                if (_readFirstGeneric)
                    throw new LexicalException("Invalid generic arguments.", invocation, Lexer.CurrentValue, Lexer.Writer);
                _readFirstGeneric = true;
            }
            else if (Lexer.CurrentValue == GenericTerminate && _readFirstGeneric)
                throw new LexicalException("Invalid generic arguments.", invocation, Lexer.CurrentValue, Lexer.Writer);
            return true;

        }

        //all errors stop the program, but these errors mean you really messed up 
        internal bool ReadMainMethod(ref int i, string invocation)
        {
            if (_beganReadingMainMethodName)
            {
                if (Lexer.ReadingGeneric)
                {
                    ReadGeneric(ref i, invocation); //readgeneric is so special isnt it, this is the only read called by another read, but it works
                    return true;
                }
                if (Lexer.CurrentValue == Whitespace)
                    while (i < invocation.Length)
                    {
                        i++;
                        Lexer.CurrentValue = invocation[i];
                        if (ValidIdentifierFirstChar(Lexer.CurrentValue) || WhitespaceEnd() || Lexer.CurrentValue == GenericTerminate)
                        {
                            if (Lexer.CurrentValue == MethodDeclr)  //this allows Method ( ) for the first/main method
                                break; //generics handle it on their own, but i need a specail case
                        }
                        else if (Lexer.CurrentValue == GenericDeclr)
                            break;
                        else if (Lexer.CurrentValue != CallLexer.Whitespace)
                            throw new LexicalException("Detected trailing input after whitespace.", invocation, Lexer.CurrentValue, Lexer.Writer, i);
                    }
                if (Lexer.CurrentValue == MethodDeclr)
                {
                    ReadMain();
                    Lexer.ReadingGeneric = false;
                    Lexer.Start = false;
                    Lexer.MethodParamsBegan = true;
                    _beganReadingMainMethodName = false;
                    return false;
                }
                if (Lexer.CurrentValue == GenericDeclr)
                    ReadGeneric<object>(invocation, null);
                else
                    Lexer.ValidIdentifier(Lexer.CurrentValue, invocation);
                return true;
            }
            else if (Lexer.CurrentValue == Whitespace)
                return false;
            if (ValidIdentifierFirstCharOrThrow(Lexer.CurrentValue, invocation))
                _beganReadingMainMethodName = true;
            return true;
        }


        void ReadMain()
        {
            MethodString method = MethodString.New(Lexer.Writer.ToString(), null, Lexer.DeclaringType!);// ?? throw new InvalidOperationException());
            Lexer.Writer.Length = 0;
            Lexer.CurrentMethod = method;
            Lexer.Main = method;

        }


        void ReadField(string invocation)
        {
            if (_readFirstCharOfName) //i expect ethod( class<int>. , ) readqualifiedmember to be true here but it is not -ah yes thats cause , causes it to be read as a field
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, Lexer.Writer);
            string typename = ResolveMemberAccess(out string fieldname)!;
            TypeString type = ImplicitDeclaredOrNew(typename);
            FieldString field = new(fieldname, type);
            Lexer.Writer.Length = 0;
            Lexer.CurrentMethod!.AddParameter(field);
            Lexer.ReadQualifiedMember = false;
        }


        void ReadMethod(string invocation)
        {
            if (_readFirstCharOfName) //i expect ethod( class<int>. , ) readqualifiedmember to be true here but it is not -ah yes thats cause , causes it to be read as a field
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, Lexer.Writer);
            string? typename = ResolveMemberAccess(out string methodname);
            TypeString type = ImplicitDeclaredOrNew(typename);
            MethodString method = MethodString.New(methodname, Lexer.CurrentMethod, type);
            Lexer.Writer.Length = 0;
            Lexer.CurrentMethod!.AddParameter(method);
            Lexer.CurrentMethod = method;
            Lexer.ReadQualifiedMember = false;
            ReadingSubparams++;
            LastReadingCount++;
        }

        TypeString ImplicitDeclaredOrNew(string? name)
        {
            if (name == null)
                return Lexer.ImplicitThis ?? throw new InvalidOperationException("Cannot use implicit this, no implicit this has been provided.");
            if (name.EqualsCaseless(Lexer.ImplicitThis?.StringID)) // == this or == _key
                return Lexer.ImplicitThis!;
            if (name.EqualsCaseless(Lexer.DeclaringType!.StringID))
                return Lexer.DeclaringType;
            return TypeString.New(name);
        }
        string? ResolveMemberAccess(out string member) //returns typename, outputs the accessed member
        {
            string qualifiedMember = Lexer.Writer.ToString();
            Lexer.Writer.Length = 0;
            return MiniLexer.ResolveMemberAccessOrFloat(qualifiedMember, out member, out _);
        }

        void CheckGenericBeforeReadGeneric(string invocation)
        {
            if (Lexer.ReadingGeneric)
                throw new LexicalException("Invalid generic arguments.", invocation, Lexer.Writer);
            Lexer.ReadingGeneric = true;
        }

        internal bool ValidIdentifierFirstCharOrThrow(char next, string invocation)
        {
            const string error = "Identifier names must Lexer.start with a letter or an underscore.";
            if (!ValidIdentifierFirstChar(next))
                throw new LexicalException(error, invocation, next, Lexer.Writer);
            return true;
        }

    }
}