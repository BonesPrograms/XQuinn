using System.Text;
using XQuinn.CodeAnalysis.AST;
using System;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis.LexicalEngine;

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


        internal const char VaidNonAlphaNumeric = '_';

        internal const char MethodStart = '(';

        internal const char MethodTerminate = ')';

        internal const char ParamTerminate = ',';

        internal const char MemberAccess = '.';

        internal const char StringDeclr = '"';

        internal const char EscSeq = '\\';

        internal const char Whitespace = ' ';

        internal const char CharDeclr = '\'';

        internal readonly StringBuilder _sb = new();

        readonly ValueReader _valueReader;

        readonly ArbitraryReader _arbitraryReader;

        internal MethodString? _main;

        internal MethodString? _currentMethod;

        internal TypeString? _declaringType;

        internal TypeString? _implicit_this;

        internal char _value;
        internal bool _start = true;

        //Primary reading rulesets - determine how to lex incoming data based on context
        internal bool _readChar;

        internal bool _readArbitraryLegalValue;
        internal bool _readQualifiedMember;
        internal bool _readDigit;
        internal bool _readString;

        // internal bool _readEnum; Not yet


        //These are supporting flags for rulesets, some rulesets have specific rules for specific characters, or need to be read around declaration characters
        internal bool _readGeneric;
        internal bool _readFirstCharOfName; //Because it cannot be a digit
        internal bool _readFloat;
        internal bool _finishedReadChar;  //Finishers/Enders are primarily for catching trailing garbage data or skipping whtiespace - ex Method("hello"  , 22, 33 s)
        internal bool _readCharValue;
        internal bool _stringEnding;         //the trailing whitespace after "hello" willbe skipped, and the trailing s after 33 will cause an exception
        internal bool _noEscape;

        internal bool _beganReadingMainMethodName; //This is a very specific flag that allows you to have leading whitespace for the main method name. Pretned | is string start. you can do |   call("hello")vb kjmhnnnnnnnnnnmm
                                                   // You need this flag to help differentiate if the whitespace is leading, or inside the method name itself, which is of course
                                                   //illegal.

        //These are for getting context on parameter values. Once a method begins or a parameter/method terminates, one of these is true, and we wait until we receive a character that gives us context on what will be read next.
        //Once we receive context, a Reading flag is set to true related to that specific context, and these flags are set to false, to prevent context getting reset in the middle of a read.
        internal bool _methodParamsBegan;
        internal bool _terminated;

        //This is for additional awareness on how deeply nested a subparameter read is
        internal int _readingSubparams;
        internal int _lastReadingCount;

        public InvokeLexer()
        {
            _valueReader = new(this);
            _arbitraryReader = new(this);
        }

        public MethodString MethodTemplate(string invocation, TypeString declaringType, TypeString? implicitAccess)
        {

            //   if (string.IsNullOrWhiteSpace(invocation))
            //   throw new ArgumentException("Invocation cannot be null or whitespace.");
            Clear();
            int? breakpoint = Breakpoint(invocation);
            _declaringType = declaringType; //this is specifically to support trycatch, though otherwise not necessary because it always clears at the end to avoid holding onto stale data
            _implicit_this = implicitAccess;
            StringBuffer(invocation, breakpoint);
            FatalLexicalError(invocation);
            MethodString method = _main!;
            Clear();
            return method;
        }

        void StringBuffer(string invocation, int? breakpoint)
        {
            int i = 0;
            while (i < invocation.Length)
            {
                if (i == breakpoint)
                    break;
                _value = invocation[i];
                if (_start)
                {
                    if (_arbitraryReader.ReadMainMethod(ref i, invocation))
                        goto Append;
                    goto Increment;
                }
                else if (_readDigit)
                {
                    if (_valueReader.ReadNum(ref i, invocation))
                        goto Append;
                }
                else if (_readString)
                {
                    if (_valueReader.ReadString(ref i, invocation))
                        goto Append;
                }
                else if (_readChar)
                {
                    if (_valueReader.ReadChar(ref i, invocation))
                        goto Append;
                }
                else if (_readArbitraryLegalValue)
                {
                    int result = _arbitraryReader.ReadArbitrary(ref i, invocation);
                    if (result == 1)
                        goto Append;
                    else if (result == 2)
                        goto Increment;
                }
                else if (_readQualifiedMember)
                {
                    if (_arbitraryReader.ReadIdentifier(ref i, invocation))
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
                        _valueReader.ReadParam();
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
        }
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


        void FatalLexicalError(string invocation)
        {
            if (_lastReadingCount > 0)
                throw new LexicalException("Nested method parameters not properly terminated. You can usually throw another ending parenthesis at the end of your invocation to fix this. ", invocation, _sb);
            if (_readGeneric) //if you dont terminate a generic with . or ( then this will throw. genericlex checks to make sure theyre propelry closed with >
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

        internal void SkipWhitespaceTrail(ref int i, string invocation)
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


        internal void ValidIdentifier(char value, string invocation, int? i)
        {
            const string error = "Detected illegal character in identifier.";//&&value!='('
            if (value != '<' && value != '>' && value != ':' && Illegal(value))
                throw i == null ? new LexicalException(error, invocation, value, _sb) : throw new LexicalException(error, invocation, value, _sb, i.Value);

        }
        public static bool Illegal(char val) => val != VaidNonAlphaNumeric && !val.IsDigit() && !val.IsLetter();
        public static bool Termination(char value) => value == ParamTerminate || value == MethodTerminate;
        public static bool ValidIdentifierFirstChar(char value) => value == VaidNonAlphaNumeric || value.IsLetter();

        void Clear() //This allows the Lexer to recover if an exception is thrown during analysis
        {             //There can also be some leftover values after a lex so state must be reset
            _main = null;
            _declaringType = null;
            _implicit_this = null;
            _value = default;
            _start = true;
            _beganReadingMainMethodName = false;
            _currentMethod = null;
            _readDigit = false;
            _readFloat = false;
            _readString = false;
            _noEscape = false;
            _readGeneric = false;
            _stringEnding = false;
            _readFirstCharOfName = false;
            _readQualifiedMember = false;
            _finishedReadChar = false;
            _readCharValue = false;
            _readChar = false;
            _readArbitraryLegalValue = false;
            _methodParamsBegan = false;
            _terminated = false;
            _readingSubparams = 0;
            _lastReadingCount = 0;
            _sb.Length = 0;
        }


        int? Breakpoint(string invocation) // a more refined version of what you see above that doesnt suffer from that invalid method breaking thing
        {                               //and is actually able to help enforce more lexical rules as to how a method is allowed to end
            bool breaking = false;      //tho atp mostly a redundancy, but this will make the exceptions more informative
            int? breakpoint = null;
            for (int i = invocation.Length - 1; i >= 0; i--)
            {
                char val = invocation[i];
                if (val == ' ')
                    continue;
                if (val == ';')
                    breaking = true;
                else if (breaking)
                {
                    if (val == MethodTerminate)
                    {
                        breakpoint = i + 1;
                        break;
                    }
                    else
                        throw new LexicalException("Invalid method termination. ", invocation, val, _sb);
                }
                else if (val == MethodTerminate)
                    break;
                else
                    throw new LexicalException("Detected trailing input after method termination or lack of method termination. ", invocation, val, _sb);
            }
            return breakpoint;
        }

        //testing with strings reveals that breakpoint needs to be pushed up by +1
        //this is because having breakpoint exactly on method terminate fucks up string closure
        // the _stringEnding branch in ReadString is not reached
        //this phenomena can be tested with class<string>.method("hi"); simply if you remove the +1 code
        //but you will get the same failed string closure exception if you do method("hi" 
        //so the cause is revealed
        //when you terminate on ), you are effectively shrinking the call down to
        //class<string>.method("hi"
        //when we reach the index after ", the loop ends entirely and we leave its scope
        //but you werent supposed to, it needs to loop *one* more time to finalize the string read
        //string read processing doesnt actually happen on the final "
        //it happens on the next loop thereafter
        //so essentially you break out of the loop and the stringread method doesnt get a chance to invoke and that causes the bug



    }









}
