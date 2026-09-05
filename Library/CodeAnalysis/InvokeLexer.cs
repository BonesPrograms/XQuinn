using System.Text;
using XQuinn.CodeAnalysis.AST;
using System.Reflection;
using System;
using XQuinn.Extensions;

namespace XQuinn.CodeAnalysis
{


    internal class LexicalException : Exception
    {
        public LexicalException(string msg, string invocation, char next, StringBuilder sb, int i) : base(msg + $"Input: {invocation} Bad character: {next} Current string value: {sb} Index: {i + 1}")
        {

        }

        public LexicalException(string msg, string invocation, StringBuilder sb) : base(msg + $"Input: {invocation} Current string value: {sb}")
        {

        }

        public LexicalException(string msg, string invocation) : base(msg + $"Input: {invocation}")
        {

        }

        public LexicalException(string msg, string invocation, char val, StringBuilder sb) : base(msg + $"Input: {invocation} Bad Character:{val} Current String Value: {sb}")
        {

        }
    }
    internal sealed class InvokeLexer
    {


        const char VaidNonAlphaNumeric = '_';

        const char MethodStart = '(';

        const char MethodTerminate = ')';

        const char ParamTerminate = ',';

        const char MemberAccess = '.';

        const char StringDeclr = '"';

        const char EscSeq = '\\';

        const char Whitespace = ' ';

        const char CharDeclr = '\'';

        readonly StringBuilder _sb = new();

        MethodString? _main;

        MethodString? _currentMethod;

        TypeString? _declaringType;

        TypeString? _implicit_this;


        char _value;
        bool _start = true;

        //Primary reading rulesets - determine how to lex incoming data based on context
        bool _readChar;
        bool _readCharValue = false;
        bool _readArbitraryLegalValue;
        bool _readQualifiedMember;
        bool _readDigit;
        bool _readString;
        bool _readGeneric;
        bool _noEscape;

        //These are supporting flags for rulesets, some rulesets have specific rules for specific characters, or need to be read around declaration characters
        bool _readFirstCharOfName;
        bool _readFloat;
        bool _finishedReadChar;  //Finishers/Enders are primarily for catching trailing garbage data or skipping whtiespace - ex Method("hello"  , 22, 33 s)
        bool _stringEnding;         //the trailing whitespace after "hello" willbe skipped, and the trailing s after 33 will cause an exception

        bool _beganReadingMainMethodName; //This is a very specific flag that allows you to have leading whitespace for the main method name. Pretned | is string start. you can do |   call("hello")vb kjmhnnnnnnnnnnmm
                                          // You need this flag to help differentiate if the whitespace is leading, or inside the method name itself, which is of course
                                          //illegal.

        //These are for getting context on parameter values. Once a method begins or a parameter/method terminates, one of these is true, and we wait until we receive a character that gives us context on what will be read next.
        //Once we receive context, a Reading flag is set to true related to that specific context, and these flags are set to false, to prevent context getting reset in the middle of a read.
        bool _methodParamsBegan;
        bool _terminated;

        int _readingSubparams;
        int _lastReadingCount; //this is used to track how deeply we are reading parameters
                               //so if readingsubparamsof == 2, we are reading a method that is the parameter of a method that is a parameter of the "main" method. ie. Method(typename:MethodTwo(typename:MethodThree())) //reading methodThree gives us a reading value of 2
                               //once were done reading (we see a Terminate op), we decrement ReadingSubParams
                               //             //and if lastreadingvalue is > readingsubparams,it lets us know "okay, we just finished reading method params, return to the method
                               //that we were reading before we started reading this one"





        //I want to explain the difference between appending, and creating a parameter real quick
        //ANd specifically, why you should not jump to append once you finish reading a parameter
        //Appending "builds up" the final "true" value as we lex it character by character. During this time, all other branches and their logic is irrelevent, each read requires
        //a specific technique.
        //Once the lexer encounters a terminator character, we consider this to be the end of the parameter
        //We no longer append (because we do not append communicators), instead we let the branch continue to the "parameter control flow"
        //The "parameter control flow" will detect that the current value is a terminator, and it will automatically
        //convert the StringBuilder to a string, which will be considered our finalized value and stored as a parameter
        //If you finish reading a parameter and you jump to append, you will violate 2 rules and cause an exception
        //1) you will append a communicator (SUPER ILLEGAL)
        //2) you will append an extra character to the parameter
        //That being said,if you do not jump to append before the parameter read is finished, you will cause things like strings to fail to parse, since they can contain terminators.
        public MethodString MethodTemplate(string invocation, TypeString declaringType, TypeString? implicitAccess)
        {

            //   if (string.IsNullOrWhiteSpace(invocation))
            //   throw new ArgumentException("Invocation cannot be null or whitespace.");
            Clear();
            int i = 0;
            _declaringType = declaringType; //this is specifically to support trycatch, though otherwise not necessary because it always clears at the end to avoid holding onto stale data
            _implicit_this = implicitAccess;
            while (i < invocation.Length)
            {
                _value = invocation[i];
                if (_start)
                {
                    if (ReadMainMethod(ref i, invocation))
                        goto Append;
                    goto Increment;
                }
                else if (_readDigit)
                {
                    if (ReadNum(ref i, invocation))
                        goto Append;
                }
                else if (_readString)
                {
                    if (ReadString(ref i, invocation))
                        goto Append;
                }
                else if (_readChar)
                {
                    if (ReadChar(ref i, invocation))
                        goto Append;
                }
                else if (_readArbitraryLegalValue)
                {
                    int result = ReadArbitrary(ref i, invocation);
                    if (result == 1)
                        goto Append;
                    else if (result == 2)
                        goto Increment;
                }
                else if (_readQualifiedMember)
                {
                    if (ReadIdentifier(ref i, invocation))
                        goto Append;
                    goto Increment;
                }
                else if (_value == Whitespace)
                    goto Increment;
                else if (_terminated || _methodParamsBegan)
                    GetContext(invocation, ref i);
                if (_lastReadingCount > _readingSubparams)
                {
                    _currentMethod = _currentMethod!._subParamOf; _lastReadingCount--;
                }
                if (_value == MethodTerminate)
                {
                    if (_readingSubparams > 0)
                        _readingSubparams--;
                }
                if (Termination(_value))
                {
                    if (_sb.Length != 0)
                    {
                        ReadParam();
                        _readArbitraryLegalValue = false;
                        _terminated = true;
                    }
                    goto Increment;
                }
            Append:
                if (!_readArbitraryLegalValue && !_readChar && !_readDigit && !_readString && !_readQualifiedMember && !_start)
                    ValidIdentifier(_value, invocation, i);
                _sb.Append(_value);
            Increment:
                i++;
            }
            FatalLexicalError(invocation);
            MethodString primary = _main!;
            Clear();
            return primary;
        }


        bool ReadChar(ref int i, string invocation)
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
                SkipWhitespaceTrail(ref i, invocation);
            if (_finishedReadChar)
            {
                _finishedReadChar = false;
                _readChar = false;
            }
            return false;
        }

        // bool ReadChar(ref int i, string invocation)
        // {
        //     const string error = "Characer declarations must be enclosed with character declaration communicators (apostrophes).";
        //     if (!_finishedReadChar)
        //     {
        //         char? next = null;
        //         try
        //         {
        //             next = invocation[i + 1];
        //         }
        //         catch (IndexOutOfRangeException)
        //         {

        //         }
        //         if (next != CharDeclr)
        //             throw next == null ? new LexicalException(error, invocation, _sb) : new LexicalException(error, invocation, next.Value, _sb, i);
        //         else
        //         {
        //             _finishedReadChar = true;
        //             i++;
        //             return true;
        //         }
        //     }
        //     else if (_value == Whitespace)
        //         SkipWhitespaceTrail(ref i, invocation);
        //     if (_finishedReadChar)
        //     {
        //         _finishedReadChar = false;
        //         _readChar = false;
        //     }
        //     return false;
        // }

        void GetContext(string invocation, ref int i) //helps us figure out whats about to be read 
        {
            if (_value == CharDeclr)
                _readChar = true;
            else if (_value == StringDeclr || _value == '@')
            {
                if (_value == '@')
                {
                    _noEscape = true;
                    if (invocation[i + 1] != '"')
                        throw new LexicalException("Invalid string format", invocation, _sb);
                    i++; //very brute forced, we check if the next key is a quote, then we jump forward, assign the quote to Value, and continue the read as we normally would for strings
                    _value = '"'; //as if we just detected a quotation mark and not an @ symbol
                }
                _readString = true;
            }
            else if (_value == '-' || _value.IsDigit())
                _readDigit = true;
            else if (ValidIdentifierFirstChar(_value))
                _readArbitraryLegalValue = true;
            if (_readDigit || _readString || _readArbitraryLegalValue || _readChar)
            {
                _terminated = false;
                _methodParamsBegan = false;
            }
        }
        int ReadArbitrary(ref int i, string invocation)
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
            else if (!Termination(_value))
                ValidIdentifier(_value, invocation, i);
            else if (_readGeneric)
                return 1; //true
            return 0; //false
        }

        bool ReadIdentifier(ref int i, string invocation) //once an arbitrary is determined to be an identifier, it is read with stricter rules
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
                ValidIdentifier(_value, invocation, i);
            return true;
        }

        //all errors stop the program, but these errors mean you really messed up 
        bool ReadMainMethod(ref int i, string invocation)
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
                ValidIdentifier(_value, invocation, i);
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

        bool ReadNum(ref int i, string invocation) //readnum doesnt influence jumps because numeric values are strict and can only contain digits/decimal pointer
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

        bool ReadString(ref int i, string invocation)
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
                i += 2;
                _value = invocation[i];
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
            if (name.EqualsCaseless(_implicit_this?.NameWithGenerics)) // == this or == _key
                return _implicit_this!;
            if (name.EqualsCaseless(_declaringType!.NameWithGenerics))
                return _declaringType;
            return TypeString.New(name);
        }

        void ReadParam()
        {
            string prm = _sb.ToString();
            ValueString param = new(prm);
            _sb.Length = 0;
            _currentMethod!.AddParameter(param);
        }
        void FatalLexicalError(string invocation)
        {
            if (_readGeneric)
                throw new LexicalException("Invalid generic arguments.", invocation, _sb);
            if (_readChar)
                throw new LexicalException("Chars require a closing apostrophe character.", invocation, _sb);
            if (_readDigit)
                throw new LexicalException("Digit parameter not terminated.", invocation, _sb);
            if (_readString)
                throw new LexicalException("Strings require a closing quotation character.", invocation, _sb);
            if (_readArbitraryLegalValue)
                throw new LexicalException("Parameter or method not terminated.", invocation, _sb);
            if (_readQualifiedMember)
                throw new LexicalException("Member access requires a terminator after the member's name; either a ( leading parenthesis for method names, or a , comma for fields.", invocation, _sb);
            if (_start)
                throw new LexicalException("Method name was unable to be read due to missing ( leading parenthesis.", invocation, _sb);
        }

        void SkipWhitespaceTrail(ref int i, string invocation)
        {
            while (i < invocation.Length)
            {
                i++;
                _value = invocation[i];
                if (Termination(_value) || ((_readQualifiedMember || _beganReadingMainMethodName) && _value == MethodStart))
                    return;
                if (_value != Whitespace)
                    throw new LexicalException("Detected trailing input after whitespace.", invocation, _value, _sb, i);
            } //if we dont do this, then values like 22 2 will parse to 222 because we otherwise skip whitespace
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

        void ValidIdentifier(char value, string invocation, int? i)
        {
            const string error = "Detected illegal character in identifier.";//&&value!='('
            if (value != '<' && value != '>' && value != ',' && value != '[' && value != ']' && value != ':' && value != '|' && Illegal(value))
                throw i == null ? new LexicalException(error, invocation, value, _sb) : throw new LexicalException(error, invocation, value, _sb, i.Value);

        }
        public static bool Illegal(char val) => val != VaidNonAlphaNumeric && !val.IsDigit() && !val.IsLetter();
        public static bool Termination(char value) => value == ParamTerminate || value == MethodTerminate;
        public static bool ValidIdentifierFirstChar(char value) => value == VaidNonAlphaNumeric || value.IsLetter();

        void Clear()
        {
            _main = null;
            _declaringType = null;
            _implicit_this = null;
            _start = true;
            _beganReadingMainMethodName = false;
            _currentMethod = null;
            _readDigit = false;
            _readFloat = false;
            _readString = false;
            _stringEnding = false;
            _readQualifiedMember = false;
            _finishedReadChar = false;
            _readCharValue=false;
            _readChar = false;
            _readArbitraryLegalValue = false;
            _methodParamsBegan = false;
            _terminated = false;
            _readingSubparams = 0;
            _lastReadingCount = 0;
            _sb.Length = 0;
        }

    }









}
