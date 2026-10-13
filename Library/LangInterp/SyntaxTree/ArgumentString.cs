namespace XQuinn.LangInterp.SyntaxTree
{
    internal abstract class ArgumentString
    {
        protected string _arg;
        internal ArgumentString(string String)
        {
            _arg = String;
        }
        public override string ToString()
        {
            return _arg;
        }
    }

    internal abstract class MetadataString : ArgumentString
    {
        public string Name => _arg;
        protected MetadataString(string arg) : base(arg)
        {
        }
    }
}
