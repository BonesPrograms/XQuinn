using System.Text;
using XQuinn.CodeAnalysis.AST;
using System;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis.LexicalEngine;

namespace XQuinn.CodeAnalysis
{


    public class LexicalException : Exception
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
    public sealed class CallLexer
    {


        public const char ValidNonAlphaNumeric = '_';

        public const char MethodDeclr = '(';

        public const char MethodTerminate = ')';

        public const char ParamTerminate = ',';

        public const char MemberAccess = '.';

        public const char StringDeclr = '"';

        public const char EscSeq = '\\';

        public const char Whitespace = ' ';

        public const char CharDeclr = '\'';

        public const char EnumOR = '|';

        public const char NoEscDeclr = '@';

        public const char GenericDeclr = '<';

        public const char GenericTerminate = '>';

        internal readonly StringBuilder Writer = new();
        internal readonly ValueReader ValueReader;
        internal readonly ArbitraryReader ArbitraryReader;
        internal MethodString? Main;
        internal MethodString? CurrentMethod;
        internal TypeString? DeclaringType;
        internal TypeString? ImplicitThis;
        public char CurrentValue;
        public bool Start = true;

        //Primary reading rulesets - determine how to lex incoming data based on context
        public bool ReadingChar;
        public bool ReadArbitraryLegalValue; //anything that isnt a digit, string or char but also hasnt yet been determined as a method field or enumOR
        public bool ReadQualifiedMember; //something that has a member access operator and is not a digit
        public bool ReadDigit;
        public bool ReadingString;
        public bool ReadingEnumOR; //OR-less enums are read as arbitrary values. | activates EnumOR read
        public bool ReadingGeneric;

        //These are supporting flags for rulesets, some rulesets have specific rules for specific characters, or need to be read around declaration characters
        public bool Skipping;

        //These are for getting context on parameter values. Once a method begins or a parameter/method terminates, one of these is true, and we wait until we receive a character that gives us context on what will be read next.
        //Once we receive context, a Reading flag is set to true related to that specific context, and these flags are set to false, to prevent context getting reset in the middle of a read.
        public bool MethodParamsBegan;
        public bool Terminated;

        //This is for additional awareness on how deeply nested a subparameter read is
        public int ReadingSubparams;
        public int LastReadCount;

        public CallLexer()
        {
            ValueReader = new(this);
            ArbitraryReader = new(this);
        }

        internal MethodString MethodTemplate(string invocation, TypeString declaringType, TypeString? implicitAccess)
        {

            //   if (string.IsNullOrWhiteSpace(invocation))
            //   throw new ArgumentException("Invocation cannot be null or whitespace.");
            Clear();
            int? breakpoint = Breakpoint(invocation);
            DeclaringType = declaringType; //this is specifically to support trycatch, though otherwise not necessary because it always clears at the end to avoid holding onto stale data
            ImplicitThis = implicitAccess;
            StringBuffer(invocation, breakpoint);
            FatalLexicalError(invocation);
            MethodString method = Main!;
            Clear();
            return method;
        }

        void StringBuffer(string invocation, int? breakpoint)
        {
            int i = 0;
            while (i < invocation.Length)
            {
                if (!Start)
                {

                }
                if (i == breakpoint)
                    break;
                CurrentValue = invocation[i];
                if (Start)
                {
                    if (ArbitraryReader.ReadMainMethod(ref i, invocation))
                        goto Append;
                    goto Increment;
                }
                else if (ReadingGeneric)
                {
                    if (ArbitraryReader.ReadGeneric(ref i, invocation))
                        goto Append;
                    goto Increment;
                }
                else if (ReadingEnumOR)
                {
                    if (ValueReader.ReadEnumOR(invocation))
                        goto Append;
                }
                else if (ReadDigit)
                {
                    if (ValueReader.ReadNum(ref i, invocation))
                        goto Append;
                }
                else if (ReadingString)
                {
                    if (ValueReader.ReadString(ref i, invocation))
                        goto Append;
                }
                else if (ReadingChar)
                {
                    if (ValueReader.ReadChar(ref i, invocation))
                        goto Append;
                }
                else if (ReadArbitraryLegalValue)
                {
                    int result = ArbitraryReader.ReadArbitrary(ref i, invocation);
                    if (result == 1)
                        goto Append;
                    else if (result == 2)
                        goto Increment;
                }
                else if (ReadQualifiedMember)
                {
                    if (ArbitraryReader.ReadIdentifier(ref i, invocation))
                        goto Append;
                    goto Increment;
                }
                else if (CurrentValue == Whitespace)
                    goto Increment;
                else if (Terminated || MethodParamsBegan)
                    GetContext();
                if (LastReadCount > ReadingSubparams)
                {
                    CurrentMethod = CurrentMethod!._subParamOf;
                    LastReadCount--;
                }
                if (CurrentValue == MethodTerminate)
                {
                    if (ReadingSubparams > 0)
                        ReadingSubparams--;
                }
                if (Termination(CurrentValue))
                {
                    if (Writer.Length != 0)
                    {
                        ValueReader.ReadParam();
                        ReadArbitraryLegalValue = false;
                        Terminated = true;

                    }

                    goto Increment;
                }
            Append:
                if (!ReadArbitraryLegalValue && !ReadingChar && !ReadDigit && !ReadingString && !ReadQualifiedMember && !Start && !ReadingEnumOR && !ReadingGeneric)
                    ValidIdentifier(CurrentValue, invocation);
                Writer.Append(CurrentValue);
            Increment:
                i++;
            }
        }
        void GetContext() //helps us figure out whats about to be read 
        {
            if (CurrentValue == CharDeclr)
                ReadingChar = true;
            else if (CurrentValue == StringDeclr || CurrentValue == NoEscDeclr)
            {
                if (CurrentValue == NoEscDeclr) //there can be an invalid sequence here but it wont throw until we get to ValueString
                {
                    ValueReader.NoEscape = true;
                    ValueReader.ReadNoEscDeclr = true;
                }
                ReadingString = true;
            }
            else if (CurrentValue == '-' || CurrentValue.IsDigit())
                ReadDigit = true;
            else if (ValidIdentifierFirstChar(CurrentValue))
                ReadArbitraryLegalValue = true;
            if (ReadDigit || ReadingString || ReadArbitraryLegalValue || ReadingChar)
            {
                Terminated = false;
                MethodParamsBegan = false;
            }
        }


        void FatalLexicalError(string invocation)
        {
            if (LastReadCount > 0)
                throw new LexicalException("Nested method parameters not properly terminated. You can usually throw another ending parenthesis at the end of your invocation to fix this. ", invocation, Writer);
            if (ReadingGeneric) //if you dont terminate a generic with . or ( then this will throw. genericlex checks later to make sure theyre propelry closed with >
                throw new LexicalException("Invalid generic arguments.", invocation, Writer);
            if (ReadingChar)
                throw new LexicalException("Chars require a closing apostrophe character.", invocation, Writer);
            if (ReadDigit)
                throw new LexicalException("Digit parameter not terminated.", invocation, Writer);
            if (ReadingString)
                throw new LexicalException("Strings require a closing quotation character.", invocation, Writer);
            if (ReadArbitraryLegalValue)
                throw new LexicalException("Parameter or method not terminated.", invocation, Writer);
            if (ReadQualifiedMember)
                throw new LexicalException("Member access requires a terminator after the member's name; either a ( leading parenthesis for method names, or a , comma for fields.", invocation, Writer);
            if (Start)
                throw new LexicalException("Method name was unable to be read due to missing ( leading parenthesis.", invocation, Writer);
        }

        public void ValidIdentifier(char value, string invocation)
        {
            const string error = "Detected illegal character in identifier.";//&&value!='('
            if (value != ':' && Illegal(value))
                throw new LexicalException(error, invocation, value, Writer);

        }
        public static bool Illegal(char val) => val != ValidNonAlphaNumeric && !val.IsDigit() && !val.IsLetter();
        public static bool Termination(char value) => value == ParamTerminate || value == MethodTerminate;
        public static bool ValidIdentifierFirstChar(char value) => value == ValidNonAlphaNumeric || value.IsLetter();

        void Clear() //This allows the Lexer to recover if an exception is thrown during analysis
        {             //There can also be some leftover values after a lex so state must be reset
            Main = null;
            DeclaringType = null;
            ImplicitThis = null;
            CurrentValue = default;
            Start = true;
            Skipping = false;
            CurrentMethod = null;
            ReadDigit = false;
            ReadingString = false;
            ReadingGeneric = false;
            Skipping = false;
            ReadQualifiedMember = false;
            ReadingChar = false;
            ReadingEnumOR = false;
            ReadArbitraryLegalValue = false;
            MethodParamsBegan = false;
            Terminated = false;
            ReadingSubparams = 0;
            LastReadCount = 0;
            Writer.Length = 0;
            ValueReader.Clear();
            ArbitraryReader.Clear();
        }
        int? Breakpoint(string invocation)
        {
            bool breaking = false;
            int? breakpoint = null;
            for (int i = invocation.Length - 1; i >= 0; i--)
            {
                char val = invocation[i];
                if (val == Whitespace)
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
                        throw new LexicalException("Invalid method termination. ", invocation, val, Writer);
                }
                else if (val == MethodTerminate)
                    break;
                else
                    throw new LexicalException("Detected trailing input after method termination or lack of method termination. ", invocation, val, Writer);
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


        ///more private notes
        /// if you have extra empty parameters ie Method(22,32,,,)
        /// the system generally ignores it
        /// we ignore it to prevent a different bug
        /// Method(22, 23, OtherMethod(), 15)
        /// ) is considered a valid parameter terminator, but so is ,
        /// so the system would, normally, try to append the 'text' between ) and ,
        /// this would not only append an empty string but it will also lead to a parameter count mismatch
        /// since , and ) both need to be able to terminate parameters, i have no way to prevent empty parameters
        /// without also recreating that original bug
        /// (we solve the bug by checking if our StringBuilder is empty before appending, which would also be the logical
        /// point to throw an 'empty parameter' exception, so doesnt really work)


    }









}
