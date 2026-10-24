using System.Text;
using System.Collections.Generic;
using System;
using XQuinn.Reflection;
using XQuinn.Extensions;

namespace XQuinn.LangInterp.SyntaxTree
{
    internal abstract class GenericString : MetadataString
    {
        public string StringID { get; private set; }
        public IReadOnlyList<TypeString> Generics
        {
            get
            {
                return _type_args == null ? Array.Empty<TypeString>() : _type_args;
            }
        }
        List<TypeString>? _type_args;
        protected GenericString(string name) : base(name)
        {
            StringID = name;
        }
        protected static T New<T>(T genericString) where T : GenericString
        {
            if (HasTypeArgs(genericString.Name))
                genericString.UpdateGenericArgs();
            return genericString;
        }

        void UpdateGenericArgs()
        {
            StringBuilder sb = new();
            LexGenerics(sb);
            sb.Length = 0;
            GenericID(sb);
        }

        static bool HasTypeArgs(string NameOrValue)
        {
            bool genericStart = false;
            bool genericEnd = false;
            int halt = -1;
            for (int i = 0; i < NameOrValue.Length; i++)
            {
                char c = NameOrValue[i];
                if (c == '<')
                {
                    halt = i;
                    genericStart = true;
                    break;
                }
            }
            if (genericStart)
            {
                for (int i = NameOrValue.Length - 1; i > halt; i--)
                {
                    char c = NameOrValue[i];
                    if (c == '>')
                    {
                        genericEnd = true;
                        break;
                    }
                }
            }
            return genericEnd || (genericStart ? throw new FormatException($"Invalid generic argument format. {NameOrValue}") : false);
        }
        protected Type[] ConvertGenericArguments(TypeBook? dic)
        {
            if (Generics.Count > 0)
            {
                Type[] genericArgs = new Type[Generics.Count];
                for (int i = 0; i < genericArgs.Length; i++)
                    genericArgs[i] = Generics[i].ToType(dic);
                return genericArgs;
            }
            throw new ArgumentException($"No generic parameters were provided to {GetType().Name} with name value {Name} ");
        }

        void LexGenerics(StringBuilder sb)
        {
            GenericString currentGeneric = this;
            bool finishedReadingLeadName = false;
            int readingsubparams = 0;
            int lastread = 0;
            for (int i = 0; i < StringID.Length; i++)
            {
                char c = StringID[i];

                if (c == CallLexer.Whitespace)
                    continue;
                if (c == CallLexer.GenericDeclr)
                {
                    if (finishedReadingLeadName)
                    {
                        readingsubparams++;
                        lastread++;
                        currentGeneric = currentGeneric.NewArg(sb);
                    }
                    else
                    {
                        currentGeneric.Arg = sb.ToString();
                        sb.Length = 0;
                        finishedReadingLeadName = true;
                    }
                }
                else if (c == CallLexer.GenericTerminate)
                {
                    if (lastread > readingsubparams)
                        lastread--;
                    if (sb.Length > 0)
                        currentGeneric.NewArg(sb);
                    if (currentGeneric != this)
                    {
                        if (currentGeneric.Generics.Count > 0)
                        {
                            currentGeneric.GenericID(sb);
                            sb.Length = 0;
                        }
                        TypeString currentArg = (TypeString)currentGeneric;
                        currentGeneric = currentArg.TypeArgOf!;
                        if (readingsubparams > 0)
                            readingsubparams--;
                        continue;

                    }
                    if (lastread > 0)
                        throw new LexicalException($"Nested generics not properly terminated. You can usually fix this by throwing an extra > at the end. Generic:", StringID);
                    return;
                }
                else if (c == CallLexer.ParamTerminate)
                {
                    if (sb.Length > 0)
                        currentGeneric.NewArg(sb);
                }
                else sb.Append(c);
            }
            throw new LexicalException("Invalid generic args", StringID);
        }

        TypeString NewArg(StringBuilder sb)
        {
            string name = sb.ToString();
            sb.Length = 0;
            _type_args ??= new List<TypeString>();
            TypeString typearg = new(name, this);
            _type_args.Add(typearg);
            return typearg;
        }

        public override string ToString()
        {
            return StringID;
        }

        void GenericID(StringBuilder sb)
        {
            sb.Append(Name);
            sb.Append('<');
            sb.AppendMany(Generics, ",");
            sb.Append('>');
            StringID = sb.ToString();
        }
    }
}
