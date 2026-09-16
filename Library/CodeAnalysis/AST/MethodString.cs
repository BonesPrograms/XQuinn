using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System;
using XQuinn.Reflection;
using XQuinn.Extensions;

namespace XQuinn.CodeAnalysis.AST
{
    internal sealed class MethodString : GenericString, IMemberString
    {


        internal readonly MethodString? _subParamOf;
        public TypeString DeclaringType => _type;
        readonly TypeString _type;
        public IReadOnlyList<ParameterString> Params
        {
            get
            {
                return _args == null ? Array.Empty<ParameterString>() : _args;
            }
        }
        List<ParameterString>? _args; //= Array.Empty<ParameterString>();
        internal static MethodString New(string name, MethodString? paramOf, TypeString declaredIn)
        {
            return New<MethodString>(new(name, paramOf, declaredIn));
        }
        MethodString(string nameForSnipping, MethodString? paramOf, TypeString type) : base(nameForSnipping)
        {
            _subParamOf = paramOf;
            _type = type;
        }

        public MethodInfo ConvertToGeneric(MethodInfo genericMethodDef, TypeBook? types = null)
        {
            //  Console.WriteLine("TConversion");
            //MethodInfo m;
            return genericMethodDef.MakeGenericMethod(ConvertGenericArguments(types));
        }
        //     catch
        //     {
        //         StringBuilder sb = new();
        //         sb.AppendMany(_generics, ", ");
        //         throw;
        //     }
        //     return m;
        // }
        internal void AddParameter(ParameterString param)
        {
            _args ??= new List<ParameterString>();
            _args.Add(param);
        }

        void ParamStringShort(StringBuilder sb)
        {
            sb.Append(StringID);
            sb.Append("( ");
            AppendParams(sb);
            sb.Append(" )");

        }

        void AppendParams(StringBuilder sb)
        {
            StringBuilder sb2 = new();
            sb.AppendMany(Params, ", ", false, x =>
            {
                if (x is MethodString ms)
                {
                    ms.ParamStringShort(sb2);
                    string s = sb2.ToString();
                    sb2.Length = 0;
                    return s;
                }
                else return x!.Argument;
            });
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(StringID);
            sb.Append($" :: Nested in ");
            _subParamOf?.ParamStringShort(sb);
            sb.Append(" :: ");
            sb.Append($"TypeName {DeclaringType.StringID} :: ");
            sb.Append("Params: ");
            if (Params.Count > 0) AppendParams(sb);
            return sb.ToString();

        }
    }
}