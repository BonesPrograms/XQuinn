namespace XQuinn.LangInterp.SyntaxTree
{
    internal abstract class ArgumentString
    {
        protected string Arg {get; set;}
        internal ArgumentString(string String)
        {
            Arg = String;
        }
        public override string ToString()
        {
            return Arg;
        }
    }

    internal abstract class MetadataString : ArgumentString
    {
        public string Name => Arg;
        protected MetadataString(string arg) : base(arg)
        {
        }
    }
}
