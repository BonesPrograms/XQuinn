namespace XQuinn.CodeAnalysis.AST
{

    internal interface IMemberString
    {
        public TypeString DeclaringType { get; }
    }

    internal sealed class FieldString : ParameterString, IMemberString
    {
        public TypeString DeclaringType => _type;
        readonly TypeString _type;
        internal FieldString(string name, TypeString declaredIn) : base(name)
        {
            _type = declaredIn;
        }

    }
}