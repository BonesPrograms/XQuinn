using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System;
using XQuinn.Reflection;
using XQuinn.Extensions;

namespace XQuinn.LangInterp.SyntaxTree
{
    internal sealed class MethodString : GenericString, IMemberString
    {
        internal readonly MethodString? _subParamOf;
        public TypeString DeclaringType => _type;
        readonly TypeString _type;
        public IReadOnlyList<ArgumentString> Params
        {
            get
            {
                return _args == null ? Array.Empty<ArgumentString>() : _args;
            }
        }
        List<ArgumentString>? _args;
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
            return genericMethodDef.MakeGenericMethod(ConvertGenericArguments(types));
        }

        internal void AddParameter(ArgumentString param)
        {
            _args ??= new List<ArgumentString>();
            _args.Add(param);
        }
        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append($"{DeclaringType.StringID}.{StringID}");

            sb.Append('(');
            sb.AppendMany(Params, ", ", false, x => x is MethodString ms ? $"{ms.DeclaringType.StringID}.{ms.StringID}()" : x!.ToString());
            sb.Append(')');
            return sb.ToString();
        }
    }
}
