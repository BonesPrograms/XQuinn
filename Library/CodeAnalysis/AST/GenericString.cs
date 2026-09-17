using System.Text;
using System.Collections.Generic;
using System;
using XQuinn.Reflection;
using XQuinn.Extensions;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.CodeAnalysis.AST
{
    internal abstract class GenericString : MetadataString
    {
        public string StringID => _id; ///Cache ID for methods and types. Includes raw name, generic arguments, and overload index.
        string _id;                     ///Used for comparison between concrete GenericString objects to see if they represent the same MemberInfo object.
                                        ///For methods, this comparison also includes comparing the StringID of their declaring type
                                        ///      (though in practice we do not typically compare that way, usually we get the actual type first)
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
            _id = name;
        }
        protected static T New<T>(T genericString) where T : GenericString
        {
            if (HasTypeArgs(genericString.Name))
                genericString.UpdateGenericArgs();

            return genericString;
        }

        protected void UpdateGenericArgs()
        {
            StringBuilder sb = new();
            LexGenerics(sb);
            sb.Length = 0;
            GenericID(sb);
        }

        internal static bool HasTypeArgs(string NameOrValue)
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
            if (genericEnd && genericStart)
                return true;
            if (genericEnd || genericStart)
                throw new FormatException($"Invalid generic argument format. {NameOrValue}");
            return false;
        }
        public Type[] ConvertGenericArguments(TypeBook? dic)
        {
            if (Generics.Count > 0)
            {
                Type[] genericArgs = new Type[Generics.Count];
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    TypeString tstring = Generics[i];
                    Type? realtype = null;
                    GenericKey key = new(tstring);
                    dic?.TryGetValue(key, out realtype);
                    realtype ??= TypeCache.GetTypeOrThrow(key);
                    if (realtype.IsGenericTypeDefinition)
                    {
                        if (RuntimeCache.s_reified_generic_types.TryGetValue(tstring.StringID, out Type? generic))
                            realtype = generic;
                        else
                        {
                            Type[] args = tstring.ConvertGenericArguments(dic);
                            realtype = realtype.MakeGenericType(args);
                            RuntimeCache.s_reified_generic_types[tstring.StringID] = realtype;
                        }
                    }
                    genericArgs[i] = realtype;

                }
                return genericArgs;
            }
            throw new ArgumentException($"No generic parameters were provided to {GetType().Name} with name value {Name} ");
        }
        void LexGenerics(StringBuilder sb)
        {
            GenericString currentGeneric = this;
            bool finishedReadingLeadName = false;
            for (int i = 0; i < _id.Length; i++) //this ones a lot simpler doesnt have smart whitespace skipping and doesnt actually check for context like
            {                                           //whether or not its reading a proper identifier and not 83528474
                char c = _id[i];                  //kinda busted it out quickly so it will allow things that the invocationlexer would throw for
                if (c == ' ')
                    continue;                 //like collecting < s t r i n g> into string or allowing impossible names to lex
                if (c == '<')
                {
                    if (finishedReadingLeadName)
                        currentGeneric = currentGeneric.NewArg(sb);
                    else
                    {
                        currentGeneric._arg = sb.ToString();
                        sb.Length = 0;
                        finishedReadingLeadName = true;
                    }
                }
                else if (c == '>')
                {
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
                        currentGeneric = currentArg._typeArgOf!;
                        continue;
                    }
                    break;
                }
                else if (c == ',')
                {
                    if (sb.Length > 0)
                        currentGeneric.NewArg(sb);
                }
                else sb.Append(c);
            }
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
            return _id;
        }

        void GenericID(StringBuilder sb)
        {
            sb.Append(Name);
            sb.Append('<');
            sb.AppendMany(Generics, ",");
            sb.Append('>');
            _id = sb.ToString();
        }




    }
}