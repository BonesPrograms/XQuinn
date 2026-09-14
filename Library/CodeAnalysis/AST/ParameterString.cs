namespace XQuinn.CodeAnalysis.AST
{
    //This is tightly coupled to CallInterpreter. It can be used elsewhere (I use it in RuntimeCommands) but actually turning parameters to objects or generic parameters to type arrays
    //relies on methods in CallInterpreter. I may decouple it later, but I found it difficult - CallInterpreter.GetGenerics() relies on CallInterpreter.
    // FindType(), and I think it is just weird to encapsulate
    //FindType(TypeString) behind GenericParameter, it should be encapsulated as part of the TypeString class, but GenericParameter would need access to that method, its just a mess rn lol
    //Parameter doesnt support Generics so you cannot just move it all up to the top class
    internal abstract class ParameterString
    {
        public string NameOrValue
        {
            get => _string;
            protected set => _string = value;
        }

        string _string;
        internal ParameterString(string String)
        {
            _string = String;
        }
        public override string ToString()
        {
            return NameOrValue;
        }


    }
}