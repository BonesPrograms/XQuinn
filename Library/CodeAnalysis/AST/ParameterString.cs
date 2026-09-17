namespace XQuinn.CodeAnalysis.AST
{
    internal abstract class ParameterString
    {
        protected string _arg;
        internal ParameterString(string String)
        {
            _arg = String;
        }
        public override string ToString()
        {
            return _arg;
        }


    }

    internal abstract class MetadataString : ParameterString 
    {
        public string Name => _arg;
        protected MetadataString(string arg) : base(arg)
        {

        }

    }
}