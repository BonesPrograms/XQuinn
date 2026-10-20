using XQuinn.LangInterp.SyntaxTree;
using XQuinn.Extensions;
using System;

using static XQuinn.LangInterp.CallLexer;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.LangInterp.LexicalEngine
{
    sealed class ArbitraryReader : LexicalObject
    {
        internal bool BeganReadingMainmethodName { get; set; }
        bool MemberAccessing { get; set; }
        bool GenericParamTerminate { get; set; }
        bool ReadFirstGeneric { get; set; }
        int GenericSubParams { get; set; }
        int LastSubCount { get; set; }
        public void Clear()
        {
            MemberAccessing = false;
            BeganReadingMainmethodName = false;
            GenericParamTerminate = false;
            ReadFirstGeneric = false;
            MemberAccessing = false;
            GenericSubParams = 0;
            LastSubCount = 0;
        }

        public ArbitraryReader(CallLexer lexer) : base(lexer)
        {
        }
        void CrossWhitespace(ref int i, string invocation, Func<bool>? pred)
        {
            while (i < invocation.Length)
            {
                i++;
                Lexer.Character = invocation[i];
                if (WhitespaceEnd() || Lexer.Character == GenericDeclr || (pred != null && pred()))
                    break;
                else if (Lexer.Character != CallLexer.Whitespace)
                    throw new LexicalException("Detected trailing input after whitespace.", invocation, Lexer.Character, Lexer.Writer, i);
            }
        }

        internal ControlFlow ReadArbitrary(ref int i, string invocation)
        {
            if (Lexer.Character == CallLexer.Whitespace)
                CrossWhitespace(ref i, invocation, delegate { return Lexer.Character == MemberAccessOrDecimal || Lexer.Character == EnumOR; });
            if (Lexer.Character == EnumOR)
            {
                ActivateReadEnumOR(invocation);
                return ControlFlow.Append;
            }
            else if (Lexer.Character == GenericDeclr)
                return ActivateReadGeneric(invocation);
            else if (Lexer.Character == MemberAccessOrDecimal)
            {
                MemberAccessing = true;
                Lexer.ReadQualifiedMember = true;
                Lexer.ReadArbitraryLegal = false;
                return ControlFlow.Append;
            }
            else if (Lexer.Character == MethodDeclr)
            {
                Lexer.ReadArbitraryLegal = false;
                return ReadMethod(invocation);
            }
            else if (!Termination(Lexer.Character))
                Lexer.ValidIdentifier(Lexer.Character, invocation);
            return ControlFlow.Release;
        }

        internal ControlFlow ReadQualifiedMember(ref int i, string invocation)
        {
            if (Lexer.Character == CallLexer.Whitespace)
                CrossWhitespace(ref i, invocation, delegate { return (MemberAccessing && Lexer.Character != CallLexer.Whitespace) || Lexer.Character == MemberAccessOrDecimal; });
            if (Lexer.Character == GenericDeclr)
                return ActivateReadGeneric(invocation);
            if (Lexer.Character == MemberAccessOrDecimal)
            {
                if (MemberAccessing)
                    throw new LexicalException("invalid name", invocation, Lexer.Writer);
                MemberAccessing = true;
                return ControlFlow.Append;
            }
            if (Termination(Lexer.Character))
                return ReadField(invocation);
            if (Lexer.Character == MethodDeclr)
                return ReadMethod(invocation);
            if (MemberAccessing)
            {
                MemberAccessing = false;
                ValidIdentifierFirstCharOrThrow(Lexer.Character, invocation);
            }
            else
                Lexer.ValidIdentifier(Lexer.Character, invocation);
            return ControlFlow.Append;
        }

        internal ControlFlow ReadGeneric(string invocation)
        {
            if (Lexer.Character == GenericTerminate)
            {
                if (LastSubCount > GenericSubParams)
                    LastSubCount--;
                if (LastSubCount == 0)
                {
                    ReadFirstGeneric = false;
                    Lexer.Skipping = false;
                    Lexer.ReadGenericArgs = false;
                    MemberAccessing = false;
                }
                if (GenericSubParams > 0)
                    GenericSubParams--;
            }
            else if (Lexer.Character == MemberAccessOrDecimal)
            {
                if (MemberAccessing)
                    throw new LexicalException("invalid name", invocation, Lexer.Writer);
                MemberAccessing = true;
            }
            else if (Lexer.Character == ParamTerminate)
            {
                if (GenericParamTerminate)
                    throw new LexicalException("Invalid generic arguments.", invocation, Lexer.Character, Lexer.Writer);
                GenericParamTerminate = true;
                Lexer.Skipping = false;
            }
            else if (Lexer.Character == CallLexer.Whitespace)
            {
                Lexer.Skipping = true;
                return ControlFlow.Increment;
            }
            else if (Lexer.Character != GenericDeclr && Lexer.Character != GenericTerminate)
            {
                if (Lexer.Skipping && !ReadFirstGeneric && !GenericParamTerminate && !MemberAccessing)
                    throw new LexicalException("Invalid generic arguments.", invocation, Lexer.Character, Lexer.Writer);
                if (MemberAccessing || (GenericParamTerminate && !ReadFirstGeneric))
                    ValidIdentifierFirstCharOrThrow(Lexer.Character, invocation);
                else
                    Lexer.ValidIdentifier(Lexer.Character, invocation);
                GenericParamTerminate = false;
                ReadFirstGeneric = false;
                Lexer.Skipping = false;
                MemberAccessing = false;
            }
            else if (Lexer.Character == GenericDeclr)
            {
                if (ReadFirstGeneric)
                    throw new LexicalException("Invalid generic arguments.", invocation, Lexer.Character, Lexer.Writer);
                ReadFirstGeneric = true;
                GenericSubParams++;
                LastSubCount++;
            }
            else if (Lexer.Character == GenericTerminate && ReadFirstGeneric)
                throw new LexicalException("Invalid generic arguments.", invocation, Lexer.Character, Lexer.Writer);
            return ControlFlow.Append;
        }

        internal ControlFlow ReadMainMethod(ref int i, string invocation)
        {
            if (BeganReadingMainmethodName)
            {
                if (Lexer.ReadGenericArgs)
                    return ReadGeneric(invocation);
                if (Lexer.Character == CallLexer.Whitespace)
                    CrossWhitespace(ref i, invocation, null);
                if (Lexer.Character == MethodDeclr)
                    return ReadMain();
                if (Lexer.Character == GenericDeclr)
                    ActivateReadGeneric(invocation);
                else
                    Lexer.ValidIdentifier(Lexer.Character, invocation);
                return ControlFlow.Append;
            }
            else if (Lexer.Character == CallLexer.Whitespace)
                return ControlFlow.Increment;
            if (ValidIdentifierFirstCharOrThrow(Lexer.Character, invocation))
                BeganReadingMainmethodName = true;
            return ControlFlow.Append;
        }

        ControlFlow ReadMain()
        {
            MethodString method = MethodString.New(Lexer.Writer.ToString(), null, Lexer.MainRef!);
            Lexer.Writer.Length = 0;
            Lexer.CurrentMethod = method;
            Lexer.Main = method;
            Lexer.MethodParamsBegan = true;
            Lexer.Start = false;
            BeganReadingMainmethodName = false;
            return ControlFlow.Increment;
        }

        ControlFlow ReadField(string invocation)
        {
            if (MemberAccessing)
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, Lexer.Writer);
            Lexer.Terminated = true;
            string typename = ResolveMemberAccess(out string fieldname)!;
            TypeString type = TypeString.New(typename);
            FieldString field = new(fieldname, type);
            Lexer.Writer.Length = 0;
            Lexer.CurrentMethod!.AddParameter(field);
            Lexer.ReadQualifiedMember = false;
            return ControlFlow.Release;
        }

        ControlFlow ReadMethod(string invocation)
        {
            if (MemberAccessing)
                throw new LexicalException("Invalid member access operator, no member was accesed.", invocation, Lexer.Writer);
            string? typename = ResolveMemberAccess(out string methodname);
            TypeString type = typename == null ? Lexer.ImplicitRef ?? throw new InvalidOperationException("Cannot use implicit this, no implicit this has been provided.") :TypeString.New(typename);
            MethodString method = MethodString.New(methodname, Lexer.CurrentMethod, type);
            Lexer.Writer.Length = 0;
            Lexer.CurrentMethod!.AddParameter(method);
            Lexer.CurrentMethod = method;
            Lexer.ReadQualifiedMember = false;
            Lexer.MethodParamsBegan = true;
            Lexer.ReadingSubParams++;
            Lexer.LastNestedReadCount++;
            return ControlFlow.Increment;
        }

        ControlFlow ActivateReadEnumOR(string invocation)
        {
            if (Lexer.ReadGenericArgs)
                throw new LexicalException("Invalid EnumOR or generic", invocation, Lexer.Character, Lexer.Writer);
            Lexer.ReadArbitraryLegal = false;
            Lexer.ReadEnumOR = true;
            Lexer.ValueReader.ReadORVal = true;
            Lexer.ValueReader.ReadORDelimit = true;
            return ControlFlow.Append;
        }

        ControlFlow ActivateReadGeneric(string invocation)
        {
            if (Lexer.ReadGenericArgs)
                throw new LexicalException("Invalid generic arguments.", invocation, Lexer.Writer);
            Lexer.ReadGenericArgs = true;
            ReadFirstGeneric = true;
            return ControlFlow.Append;
        }

        string? ResolveMemberAccess(out string member)
        {
            string qualifiedMember = Lexer.Writer.ToString();
            Lexer.Writer.Length = 0;
            return MiniLexer.ResolveMemberAccessOrLiteral(qualifiedMember, out member, out _);
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
