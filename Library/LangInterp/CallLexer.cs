using System.Text;
using XQuinn.LangInterp.SyntaxTree;
using System;
using XQuinn.LangInterp.LexicalEngine;
using System.Diagnostics;

namespace XQuinn.LangInterp
{
    internal sealed class CallLexer
    {
        internal const char ValidNonAlphaNumeric = '_';
        internal const char MethodDeclr = '(';
        internal const char MethodTerminate = ')';
        internal const char ParamTerminate = ',';
        internal const char MemberAccessOrDecimal = '.';
        internal const char StringDeclr = '"';
        internal const char NegSign = '-';
        internal const char EscSeq = '\\';
        internal const char CharDeclr = '\'';
        internal const char EnumOR = '|';
        internal const char NoEscDeclr = '@';
        internal const char GenericDeclr = '<';
        internal const char GenericTerminate = '>';
        internal const char Break = ';';
        internal static Whitespace Whitespace => Whitespace._whitespace;
        internal StringBuilder Writer { get; private init; } = new();
        internal ValueReader ValueReader { get; private init; }
        internal ArbitraryReader ArbitraryReader { get; private init; }
        internal MethodString? Main { get; set; }
        internal MethodString? CurrentMethod { get; set; }
        internal TypeString? MainRef { get; private set; }
        internal TypeString? ImplicitRef { get; private set; }
        internal char Character { get; set; }
        internal bool Start { get; set; } = true;
        internal bool ReadChar { get; set; }
        internal bool ReadArbitraryLegal { get; set; }
        internal bool ReadQualifiedMember { get; set; }
        internal bool ReadDigit { get; set; }
        internal bool ReadString { get; set; }
        internal bool ReadEnumOR { get; set; }
        internal bool ReadGenericArgs { get; set; }
        internal bool Skipping { get; set; }
        internal bool MethodParamsBegan { get; set; }
        internal bool Terminated { get; set; }
        internal int ReadingSubParams { get; set; }
        internal int LastNestedReadCount { get; set; }
        Read ReadState
        {
            get
            {
                if (Start)
                    return Read.Start;
                if (ReadGenericArgs)
                    return Read.ReadGeneric;
                if (ReadEnumOR)
                    return Read.ReadEnumOR;
                if (ReadString)
                    return Read.ReadString;
                if (ReadChar)
                    return Read.ReadChar;
                if (ReadDigit)
                    return Read.ReadDigit;
                if (ReadArbitraryLegal)
                    return Read.ReadArbitrary;
                if (ReadQualifiedMember)
                    return Read.ReadQualified;
                if (Character == Whitespace)
                    return Read.Whitespace;
                if (Terminated || MethodParamsBegan)
                    return Read.Context;
                return Read.Releasing;
            }
        }

        internal CallLexer()
        {
            ValueReader = new(this);
            ArbitraryReader = new(this);
        }

        internal MethodString MethodTemplate(string invocation, TypeString declaringType, TypeString? implicitRef)
        {
            Clear();
            int breakpoint = Breakpoint(invocation) ?? invocation.Length;
            MainRef = declaringType;
            ImplicitRef = implicitRef;
            StringBuffer(invocation, breakpoint);
            FatalLexicalError(invocation);
            MethodString method = Main!;
            Clear();
            return method;
        }

        void StringBuffer(string invocation, int breakpoint)
        {
            int i = 0;
            while (i < breakpoint)
            {
                Character = invocation[i];
                ControlFlow flow = ReadState switch
                {
                    Read.Start => ArbitraryReader.ReadMainMethod(ref i, invocation),
                    Read.ReadGeneric => ArbitraryReader.ReadGeneric(invocation),
                    Read.ReadEnumOR => ValueReader.ReadEnumOR(invocation),
                    Read.ReadDigit => ValueReader.ReadNum(ref i, invocation),
                    Read.ReadString => ValueReader.ReadString(ref i, invocation),
                    Read.ReadChar => ValueReader.ReadChar(ref i, invocation),
                    Read.ReadArbitrary => ArbitraryReader.ReadArbitrary(ref i, invocation),
                    Read.ReadQualified => ArbitraryReader.ReadQualifiedMember(ref i, invocation),
                    Read.Context => GetContext(),
                    Read.Whitespace => ControlFlow.Increment,
                    Read.Releasing => ControlFlow.Release,
                    _ => throw new UnreachableException()
                };
                if (flow == ControlFlow.Append)
                    goto Append;
                if (flow == ControlFlow.Increment)
                    goto Increment;
                if (LastNestedReadCount > ReadingSubParams)
                {
                    CurrentMethod = CurrentMethod!.SubParamOf;
                    LastNestedReadCount--;
                }
                if (Character == MethodTerminate)
                {
                    if (ReadingSubParams > 0)
                        ReadingSubParams--;
                }
                if (Termination(Character))
                {
                    if (Writer.Length != 0)
                    {
                        ValueReader.ReadParam();
                        ReadArbitraryLegal = false;
                        Terminated = true;
                    }
                    goto Increment;
                }
                ValidIdentifier(Character, invocation);
            Append:
                Writer.Append(Character);
            Increment:
                i++;
            }
        }

        ControlFlow GetContext()
        {
            if (Character == CharDeclr)
                ReadChar = true;
            else if (Character == StringDeclr || Character == NoEscDeclr)
            {
                if (Character == NoEscDeclr)
                {
                    ValueReader.NoEscape = true;
                    ValueReader.ReadNoEscDeclr = true;
                }
                ReadString = true;
            }
            else if (Character == NegSign || Character == MemberAccessOrDecimal || char.IsDigit(Character))
            {
                ReadDigit = true;
                if (Character == MemberAccessOrDecimal)
                    ValueReader.ReadFloat = true;
            }
            else if (ValidIdentifierFirstChar(Character))
                ReadArbitraryLegal = true;
            if (ReadDigit || ReadString || ReadArbitraryLegal || ReadChar)
            {
                Terminated = false;
                MethodParamsBegan = false;
                return ControlFlow.Append;
            }
            return ControlFlow.Release;
        }

        void FatalLexicalError(string invocation)
        {
            if (LastNestedReadCount > 0)
                throw new LexicalException("Nested method parameters not properly terminated. You can usually throw another ending parenthesis at the end of your invocation to fix this. ", invocation, Writer);
            if (ReadGenericArgs)
                throw new LexicalException("Invalid generic arguments.", invocation, Writer);
            if (ReadChar)
                throw new LexicalException("Chars require a closing apostrophe character.", invocation, Writer);
            if (ReadDigit)
                throw new LexicalException("Digit parameter not terminated.", invocation, Writer);
            if (ReadString)
                throw new LexicalException("Strings require a closing quotation character.", invocation, Writer);
            if (ReadArbitraryLegal)
                throw new LexicalException("Parameter or method not terminated.", invocation, Writer);
            if (ReadQualifiedMember)
                throw new LexicalException("Member access requires a terminator after the member's name; either a ( leading parenthesis for method names, or a , comma for fields.", invocation, Writer);
            if (Start)
                throw new LexicalException("Method name was unable to be read due to missing ( leading parenthesis.", invocation, Writer);
        }

        internal void ValidIdentifier(char value, string invocation)
        {
            if (value != ':' && Illegal(value))
                throw new LexicalException("Detected illegal character in identifier.", invocation, value, Writer);
        }
        internal static bool Illegal(char val) => val != ValidNonAlphaNumeric && !char.IsDigit(val) && !char.IsLetter(val);
        internal static bool Termination(char value) => value == ParamTerminate || value == MethodTerminate;
        internal static bool ValidIdentifierFirstChar(char value) => value == ValidNonAlphaNumeric || char.IsLetter(value);

        void Clear()
        {
            Main = null;
            MainRef = null;
            ImplicitRef = null;
            Character = default;
            Start = true;
            Skipping = false;
            CurrentMethod = null;
            ReadDigit = false;
            ReadString = false;
            ReadGenericArgs = false;
            Skipping = false;
            ReadQualifiedMember = false;
            ReadChar = false;
            ReadEnumOR = false;
            ReadArbitraryLegal = false;
            MethodParamsBegan = false;
            Terminated = false;
            ReadingSubParams = 0;
            LastNestedReadCount = 0;
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
                if (val == Break)
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
                {
                    breakpoint = i + 1;
                    break;
                }
                else
                    throw new LexicalException("Detected trailing input after method termination or lack of method termination. ", invocation, val, Writer);
            }
            return breakpoint;
        }

        enum Read
        {
            Releasing,
            Start,
            ReadGeneric,
            ReadEnumOR,
            ReadString,
            ReadDigit,
            ReadChar,
            ReadArbitrary,
            ReadQualified,
            Whitespace,
            Context
        }
    }

    internal sealed class Whitespace
    {
        internal static readonly Whitespace _whitespace = new();
        public override bool Equals(object? obj)
        {
            return obj is char c && char.IsWhiteSpace(c);
        }

        public static bool operator ==(char left, Whitespace right) => char.IsWhiteSpace(left);

        public static bool operator !=(char left, Whitespace right) => !(left == right);

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        Whitespace()
        {
        }
    }

    internal sealed class LexicalException : Exception
    {
        internal LexicalException(string msg, string invocation, char next, StringBuilder sb, int i) : base(msg + $"Input: {invocation} Bad character: {next} Current string value: {sb} Index: {i + 1}")
        {
        }

        internal LexicalException(string msg, string invocation, StringBuilder sb) : base(msg + $"Input: {invocation} Current string value: {sb}")
        {
        }

        internal LexicalException(string msg, string invocation) : base(msg + $"Input: {invocation}")
        {
        }

        internal LexicalException(string msg, string invocation, char val, StringBuilder sb) : base(msg + $"Input: {invocation} Bad Character:{val} Current String Value: {sb}")
        {
        }
    }
}
