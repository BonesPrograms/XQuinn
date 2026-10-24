using XQuinn.LangInterp.SyntaxTree;
using System;
using static XQuinn.LangInterp.CallLexer;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.LangInterp.LexicalEngine
{
    sealed class ArbitraryReader : LexicalObject
    {
        internal bool BeganReadingMainmethodName { get; private set; }
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

        internal ControlFlow ReadArbitrary()
        {
            if (Lexer.Character == CallLexer.Whitespace)
                Lexer.CrossWhitespace(delegate { return Lexer.Character == GenericDeclr || Lexer.Character == MemberAccessOrDecimal || Lexer.Character == EnumOR; });
            if (Lexer.Character == EnumOR)
                return ActivateReadEnumOR();
            else if (Lexer.Character == GenericDeclr)
                return ActivateReadGeneric();
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
                return ReadMethod();
            }
            return ControlFlow.Release;
        }

        internal ControlFlow ReadQualifiedMember()
        {
            if (Lexer.Character == CallLexer.Whitespace)
                Lexer.CrossWhitespace(delegate { return Lexer.Character == GenericDeclr || (MemberAccessing && Lexer.Character != CallLexer.Whitespace) || Lexer.Character == MemberAccessOrDecimal; });
            if (Lexer.Character == GenericDeclr)
                return ActivateReadGeneric();
            if (Lexer.Character == MemberAccessOrDecimal)
            {
                if (MemberAccessing)
                    throw new LexicalException("invalid name", Lexer.invocation, Lexer.Buffer);
                MemberAccessing = true;
                return ControlFlow.Append;
            }
            if (Lexer.Termination())
                return ReadField();
            if (Lexer.Character == MethodDeclr)
                return ReadMethod();
            if (MemberAccessing)
            {
                MemberAccessing = false;
                ValidIdentifierFirstCharOrThrow();
                return ControlFlow.Append;
            }
            return ControlFlow.Release;
        }

        internal ControlFlow ReadGeneric()
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
                    throw new LexicalException("invalid name", Lexer.invocation, Lexer.Buffer);
                MemberAccessing = true;
            }
            else if (Lexer.Character == ParamTerminate)
            {
                if (GenericParamTerminate)
                    throw new LexicalException("Invalid generic arguments.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
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
                    throw new LexicalException("Invalid generic arguments.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
                if (MemberAccessing || (GenericParamTerminate && !ReadFirstGeneric))
                    ValidIdentifierFirstCharOrThrow();
                else
                    Lexer.ValidIdentifier();
                GenericParamTerminate = false;
                ReadFirstGeneric = false;
                Lexer.Skipping = false;
                MemberAccessing = false;
            }
            else if (Lexer.Character == GenericDeclr)
            {
                if (ReadFirstGeneric)
                    throw new LexicalException("Invalid generic arguments.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
                ReadFirstGeneric = true;
                GenericSubParams++;
                LastSubCount++;
            }
            else if (Lexer.Character == GenericTerminate && ReadFirstGeneric)
                throw new LexicalException("Invalid generic arguments.", Lexer.invocation, Lexer.Character, Lexer.Buffer);
            return ControlFlow.Append;
        }

        internal ControlFlow ReadMainMethod()
        {
            if (BeganReadingMainmethodName)
            {
                if (Lexer.ReadGenericArgs)
                    return ReadGeneric();
                if (Lexer.Character == CallLexer.Whitespace)
                    Lexer.CrossWhitespace(delegate {return Lexer.Character == GenericDeclr;});
                if (Lexer.Character == MethodDeclr)
                    return ReadMain();
                if (Lexer.Character == GenericDeclr)
                    ActivateReadGeneric();
                else
                    Lexer.ValidIdentifier();
                return ControlFlow.Append;
            }
            else if (Lexer.Character == CallLexer.Whitespace)
                return ControlFlow.Increment;
            if (ValidIdentifierFirstCharOrThrow())
                BeganReadingMainmethodName = true;
            return ControlFlow.Append;
        }

        ControlFlow ReadMain()
        {
            MethodString method = MethodString.New(Lexer.Buffer.ToString(), null, Lexer.MainRef!);
            Lexer.Buffer.Length = 0;
            Lexer.CurrentMethod = method;
            Lexer.Main = method;
            Lexer.MethodParamsBegan = true;
            Lexer.Start = false;
            BeganReadingMainmethodName = false;
            return ControlFlow.Increment;
        }

        ControlFlow ReadField()
        {
            if (MemberAccessing)
                throw new LexicalException("Invalid member access operator, no member was accesed.", Lexer.invocation, Lexer.Buffer);
            Lexer.Terminated = true;
            string typename = ResolveMemberAccess(out string fieldname)!;
            TypeString type = TypeString.New(typename);
            FieldString field = new(fieldname, type);
            Lexer.Buffer.Length = 0;
            Lexer.CurrentMethod!.AddParameter(field);
            Lexer.ReadQualifiedMember = false;
            return ControlFlow.Release;
        }

        ControlFlow ReadMethod()
        {
            if (MemberAccessing)
                throw new LexicalException("Invalid member access operator, no member was accesed.", Lexer.invocation, Lexer.Buffer);
            string? typename = ResolveMemberAccess(out string methodname);
            TypeString type = typename == null ? Lexer.ImplicitRef ?? throw new InvalidOperationException("Cannot use implicit this, no implicit this has been provided.") : TypeString.New(typename);
            MethodString method = MethodString.New(methodname, Lexer.CurrentMethod, type);
            Lexer.Buffer.Length = 0;
            Lexer.CurrentMethod!.AddParameter(method);
            Lexer.CurrentMethod = method;
            Lexer.ReadQualifiedMember = false;
            Lexer.MethodParamsBegan = true;
            Lexer.ReadingSubParams++;
            Lexer.LastNestedReadCount++;
            return ControlFlow.Increment;
        }

        ControlFlow ActivateReadEnumOR()
        {
            if (Lexer.ReadGenericArgs)
                throw new LexicalException("Invalid EnumOR or generic", Lexer.invocation, Lexer.Character, Lexer.Buffer);
            Lexer.ReadArbitraryLegal = false;
            Lexer.ReadEnumOR = true;
            Lexer.ValueReader.ReadORVal = true;
            Lexer.ValueReader.ReadORDelimit = true;
            return ControlFlow.Append;
        }

        ControlFlow ActivateReadGeneric()
        {
            if (Lexer.ReadGenericArgs)
                throw new LexicalException("Invalid generic arguments.", Lexer.invocation, Lexer.Buffer);
            Lexer.ReadGenericArgs = true;
            ReadFirstGeneric = true;
            return ControlFlow.Append;
        }

        string? ResolveMemberAccess(out string member)
        {
            string qualifiedMember = Lexer.Buffer.ToString();
            Lexer.Buffer.Length = 0;
            return SimpleLex.ResolveMemberAccessOrLiteral(qualifiedMember, out member, out _);
        }

        internal bool ValidIdentifierFirstCharOrThrow()
        {
            char next = Lexer.Character;
            const string error = "Identifier names must Lexer.start with a letter or an underscore.";
            if (!Lexer.ValidIdentifierFirstChar())
                throw new LexicalException(error, Lexer.invocation, next, Lexer.Buffer);
            return true;
        }
    }
}
