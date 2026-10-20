namespace XQuinn.LangInterp.SyntaxTree
{
    internal interface IMemberString
    {
        public TypeString DeclaringType { get; }
    }

    internal sealed class FieldString : MetadataString, IMemberString
    {
        public TypeString DeclaringType { get; private init; }
        internal FieldString(string name, TypeString declaredIn) : base(name)
        {
            DeclaringType = declaredIn;
        }

        public override string ToString()
        {
            return $"{DeclaringType.StringID}.{Name}";
        }
    }
}
