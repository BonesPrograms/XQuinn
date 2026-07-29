using System.Text;
using XQuinn.Parsing.AST;
using System.Reflection;

namespace XQuinn.Parsing;


public class LexicalException : Exception
{
    public LexicalException(string msg, string invocation, char next, StringBuilder sb, int i) : base(msg + $"Input: {invocation} Bad character: {next} Current string value: {sb} Index: {i + 1}")
    {

    }

    public LexicalException(string msg, string invocation, StringBuilder sb) : base(msg + $"Input: {invocation} Current string value: {sb}")
    {

    }

    public LexicalException(string msg, string invocation, char val, StringBuilder sb) : base(msg + $"Input: {invocation} Bad Character:{val} Current String Value: {sb}")
    {

    }
}
public sealed class InvocationLexer
{
    // static readonly HashSet<char> Alphabet = new()
    // {
    //   'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
    //   'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',

    // };



    // static readonly HashSet<char> Numbers = new()
    // {
    //     '1','2','3','4','5','6','7','8','9', '0'
    // };
    // static readonly HashSet<char> Communicators = typeof(InvocationLexer)
    // .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
    // .Where(x => x.IsLiteral && x.FieldType == typeof(char))
    // .Select(x => (char)x.GetValue(null)!)
    // .ToHashSet();



    const char VaidNonAlphaNumeric = '_'; //underscores are valid as part of identifier names
    const char ValidLeadingNonAlphaNumeric = '@'; //this isnt allowed except at the very beginning of an identifier name
    const char MethodStart = '(';

    const char MethodTerminate = ')';

    const char ParamTerminate = ',';

    const char MemberAccess = '.';

    const char StringDeclr = '"';

    const char EscSeq = '\\';

    const char Whitespace = ' ';

    const char CharDeclr = '\'';

    readonly StringBuilder sb = new();

    Method? Main;

    Method? CurrentMethod;

    char Value;

    bool FinishedReadChar;
    bool ReadingChar;
    bool BeganFirstRead;
    bool ReadingArbitrary;
    bool ReadingDigit;
    bool Start;
    bool ReadFloat;
    bool MethodBegan;
    bool MethodTerminated;
    bool ReadingString;

    bool EndString;

    bool ReadingIdentifier;

    int ReadingSubparamsOf;

    int LastReadingValue; //this is used to track how deeply we are reading parameters
                          //so if readingsubparamsof == 2, we are reading a method that is the parameter of a method that is a parameter of the "main" method. ie. Method(typename:MethodTwo(typename:MethodThree())) //reading methodThree gives us a reading value of 2
                          //once were done reading (we see a Terminate op), we decrement ReadingSubParams
                          //             //and if lastreadingvalue is > readingsubparams,it lets us know "okay, we just finished reading method params, return to the method
                          //that we were reading before we started reading this one"

    //I want to explain the difference between appending, and creating a parameter real quick
    //ANd specifically, why you should not jump to append once you finish reading a parameter
    //Appending "builds up" the final "true" value as we lex it character by character. During this time, all other branches and their logic is irrelevent, each read requires
    //a specific technique.
    //Once the lexer encounters a terminator character, we consider this to be the end of the parameter
    //We no longer append (because we do not append communicators), instead we let the branch continue to the "parameter control flow"
    //The "parameter control flow" will detect that the current value is a terminator, and it will automatically
    //convert the StringBuilder to a string, which will be considered our finalized value and stored as a parameter
    //If you finish reading a parameter and you jump to append, you will violate 2 rules and cause an exception
    //1) you will append a communicator (SUPER ILLEGAL)
    //2) you will append an extra character to the parameter
    //That being said,if you do not jump to append before the parameter read is finished, you will cause things like strings to fail to parse, since they can contain terminators.
    public Method ParameterTemplate(string invocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocation, nameof(invocation));
        Clear();
        int i = 0;
        while (i < invocation.Length)
        {
            Value = invocation[i];
            if (Start) //Method(Param1,Param2)
            {
                if (ReadMainMethod(ref i, invocation))
                    goto Append;
                else
                    goto Increment;
            }
            else if (ReadingDigit)
            {
                if (ReadNum(ref i, invocation))
                    goto Append; //both of these skip to append while building, but once theyre finished, parameter control flow will take over
            }
            else if (ReadingString)
            {
                if (ReadString(ref i, invocation))
                    goto Append;
            }
            else if (ReadingChar)
            {
                if (ReadChar(ref i, invocation))
                    goto Append;
            }
            else if (ReadingArbitrary)
            {
                if (ReadArbitrary(ref i, invocation))
                    goto Append;
            }
            else if (ReadingIdentifier)
            {
                if (ReadIdentifier(ref i, invocation))
                    goto Append;
                else
                    goto Increment;

            }
            else if (Value == Whitespace)
                goto Increment;
            else if (MethodTerminated || MethodBegan)
            {
                GetContext();
                if (ReadingChar) //chars are read in a special way
                    goto Increment; //currently the Char Value is CharDeclr, we do not append char declaration communicators, so we skip this one
            }
            if (LastReadingValue > ReadingSubparamsOf)// && CurrentMethod!._nest != null) //not necessary because the reading counter prevents this by its logic
            {
                CurrentMethod = CurrentMethod!._paramOf;
                LastReadingValue--;
                goto Increment; //this prevents a bug where the system reads an empty char in a specific circumstance - when terminating a method that is a parameter. so if your call looks like Method(22, "hello", GetMethod(), 15) - the system wouldve stored the empty space between ) and , after GetMethod( . now it skips storing the empty slot
            }
            if (Value == MethodTerminate)
            {
                if (ReadingSubparamsOf > 0)
                    ReadingSubparamsOf--;
            }
            if (Termination(Value))
            {
                ReadParam();
                ReadingArbitrary = false;
                MethodTerminated = true;
                goto Increment;

            }
        Append:
            if (!ReadingString && !ReadingDigit && !ReadingChar) //ReadingDigit already handles illegals, ReadChar and ReadString can accept any illegal character
                ValidIdentifier(Value, invocation, i);
            sb.Append(Value);
        Increment:
            i++;
        }
        FatalLexicalError(invocation);
        return Main!;
    }

    bool ReadChar(ref int i, string invocation)
    {
        const string error = $"Characer declarations must be enclosed with character declaration communicators (apostrophes).";
        if (!FinishedReadChar)
        {
            char? next = null;
            try
            {
                next = invocation[i + 1];
            }
            catch (IndexOutOfRangeException)
            {

            }
            if (next != CharDeclr)
            {
                if (next == null)
                    throw new LexicalException(error, invocation, sb);
                else
                    throw new LexicalException(error, invocation, next.Value, sb, i);
            }
            else
            {
                FinishedReadChar = true;
                i++;
                return true;
            }
        }
        else if (Value == Whitespace)
        {
            SkipWhitespaceTrail(ref i, invocation);
        }
        if (FinishedReadChar)
        {
            ReadingChar = false;
        }
        return false;
    }

    void GetContext() //helps us figure out whats about to be read 
    {
        if (Value == CharDeclr)
            ReadingChar = true;
        else if (Value == StringDeclr)
            ReadingString = true;
        else if (IsDigit(Value))
            ReadingDigit = true;
        else if (ValidIdentifierFirstChar(Value))
            ReadingArbitrary = true; //enums, bools, identifiers and true arbitrary values are *initially* read as arbitraries. 
        if (ReadingDigit || ReadingString || ReadingArbitrary || ReadingChar)//if all these bools are false, that means we havent read anything of value, and can continue waiting for context
        {
            MethodTerminated = false;
            MethodBegan = false;
        }
    }
    bool ReadArbitrary(ref int i, string invocation) //this could be an enum, bool, identifier or a true arbitrary. 
    {                                                  //a true arbitrary is an input value that is not yet representing a data type, a string, a field, or a method. in essence it does not represent a valid C# construct
        if (Value == Whitespace)                        //true arbitraries will always fail to parse in call interpreter without special overloads to support their value
            SkipWhitespaceTrail(ref i, invocation);
        if (Value == MemberAccess)
        {
            ValidMemberAccess(invocation, i);
            ReadingIdentifier = true;
            ReadingArbitrary = false;
            return true;
        }
        return false;
    }

    bool ReadIdentifier(ref int i, string invocation) //once an arbitrary is determined to be an identifier, it is read with stricter rules
    {
        if (Value == Whitespace)
            SkipWhitespaceTrail(ref i, invocation);
        if (Value == MemberAccess)
        {
            ValidMemberAccess(invocation, i);
            return true;
        }
        if (Termination(Value))
        {
            MethodTerminated = true;
            ReadField(invocation);
            return false;
        }
        if (Value == MethodStart)
        {
            MethodBegan = true;
            ReadMethod(invocation); //unfortunately i cant know at compile time if you insert one of these 4 communicators into your identifier mistakenly, or if youre using it as a terminator/namespace accessor
            return false;           //so if you mess that up, it will fail to resolve somwhere in CallInterpreter, you will either get a parameter count mismatch, or it will say it cannot find the type/member with that name.
        }
        return true;
    }

    //all errors stop the program, but these errors mean you really messed up 
    bool ReadMainMethod(ref int i, string invocation)
    {
        if (BeganFirstRead)
        {
            if (Value == Whitespace)
            {
                SkipWhitespaceTrail(ref i, invocation);
            }
            if (Value == MethodStart)
            {
                ReadMain();
                Start = false;
                MethodBegan = true;
                BeganFirstRead = false;
                return false;
            }
            ValidIdentifier(Value, invocation, i);
            return true;
        }
        else if (Value == Whitespace)
            return false;
        if (ValidIdentifierFirstCharOrThrow(Value, invocation, i))
            BeganFirstRead = true;
        return true;
    }

    void ReadMain()
    {
        //reads everything prior to (
        Method method = new(sb.ToString());
        sb.Length = 0;
        CurrentMethod = method;
        Main = method;

    }

    bool ReadNum(ref int i, string invocation) //readnum doesnt influence jumps because numeric values are strict and can only contain digits/decimal pointer
    {
        if (IsDigit(Value))
            return true;
        else if (Value == Whitespace) //we skip leading and trailing whitespace
        {
            SkipWhitespaceTrail(ref i, invocation);
            ReadingDigit = false;
            ReadFloat = false;
            return false;
        }
        else //nondigit value
        {
            if (Value == MemberAccess)
            {
                if (ReadFloat)
                    throw new LexicalException("Floats cannot contain multiple periods.", invocation, Value, sb, i);
                ReadFloat = true;
                return true;
            }
            else if (Termination(Value))
            {
                ReadingDigit = false;
                ReadFloat = false;
                return false;
            }
            else
                throw new LexicalException("Numbers can only contain digits or one period.", invocation, Value, sb, i);
        }
    }

    bool ReadString(ref int i, string invocation)
    {

        if (EndString)
        {
            if (Value == Whitespace)
                SkipWhitespaceTrail(ref i, invocation);
            EndString = false;
            ReadingString = false;
            return false; //return false allows parameter control flow to takeover
        }
        else if (Value == EscSeq)
        {
            i++;
            Value = invocation[i];
            return true;
        }
        else if(Value == StringDeclr)
        {
            EndString = true;
        }
        return true; //skips parameter control flow, appends
    }
    // static bool IsCommunicator(char val) => val switch
    // {
    //     MethodStart or MethodTerminate or ParamTerminate or MemberAccess or StringDeclr or EscSeq or CharDeclr => true,
    //     _=>false
    // };


    void ReadField(string invocation)
    {
        string typename = SplitMemberAccess(invocation, out string fieldname);
        TypeString type = new(typename);
        Field field = new(fieldname, CurrentMethod, type);
        sb.Length = 0;
        CurrentMethod!.Add(field);
        ReadingIdentifier = false;
    }


    void ReadMethod(string invocation)
    {
        string typename = SplitMemberAccess(invocation, out string methodname);
        TypeString type = new(typename);
        Method method = new(methodname, CurrentMethod, type);
        sb.Length = 0;
        CurrentMethod!.Add(method);
        CurrentMethod = method;
        ReadingIdentifier = false;
        ReadingSubparamsOf++;
        LastReadingValue++;
    }

    void ReadParam()
    {
        string prm = sb.ToString();
        Parameter param = new(prm, CurrentMethod);
        sb.Length = 0;
        CurrentMethod!.Add(param);
    }
    void FatalLexicalError(string invocation)
    {
        if (ReadingChar)
            throw new LexicalException("Chars require a closing apostrophe character.", invocation, sb);
        if (ReadingDigit)
            throw new LexicalException("Digit parameter not terminated.", invocation, sb);
        if (ReadingString)
            throw new LexicalException("Strings require a closing quotation character.", invocation, sb);
        if (ReadingArbitrary)
            throw new LexicalException("Parameter or method not terminated.", invocation, sb);
        if (ReadingIdentifier)
            throw new LexicalException("Member access requires a terminator after the member's name; either a ( leading parenthesis for method names, or a , comma for fields.", invocation, sb);
        if (Start)
            throw new LexicalException("Method name was unable to be read due to missing ( leading parenthesis.", invocation, sb);
    }

    void SkipWhitespaceTrail(ref int i, string invocation)
    {
        while (i < invocation.Length)
        {
            i++;
            Value = invocation[i];
            if (Termination(Value))
                return;
            else if (Value != Whitespace)
                throw new LexicalException("Detected trailing input after whitespace.", invocation, Value, sb, i);
        } //if we dont do this, then values like 22 2 will parse to 222 because we otherwise skip whitespace
    }

    string SplitMemberAccess(string invocation, out string member) //returns typename, outputs the accessed member
    {
        string lexOutput = sb.ToString();
        sb.Length = 0;
        int lastAccessorIndex = 0;//this value will never be invalid, there will always be at least one memberaccess communicator
        ValidIdentifierFirstCharOrThrow(lexOutput[0], invocation, null);
        for (int i = 0; i < lexOutput.Length; i++)
        {
            char val = lexOutput[i];
            if (val == MemberAccess)
            {
                lastAccessorIndex = i;
                val = lexOutput[i + 1];
                ValidIdentifierFirstCharOrThrow(val, invocation, null);
                i++;
            }
            else if (i != 0)
                ValidIdentifier(val, invocation, null);
        }
        member = lexOutput.Substring(lastAccessorIndex + 1);
        string typename = lexOutput.Remove(lastAccessorIndex);
        return typename;
    }
    bool ValidMemberAccess(string invocation, int i)
    {
        char next = invocation[i + 1]; //this reads ahead but does not increment the index because the next value will need to be appended and append always increments, so you will end up skipping ahead of a character and missing it
        return ValidIdentifierFirstCharOrThrow(next, invocation, i); //it occurs to me now that we could do it properly by doing sb.Append right inside of here, along with some other stuff but i dont want to cause none of the other methods do it directly
    }

    bool ValidIdentifierFirstCharOrThrow(char next, string invocation, int? i)
    {
        const string error = "Identifier names must start with a letter, @ or an underscore.";
        if (!ValidIdentifierFirstChar(next))
        {
            if (i == null)
                throw new LexicalException(error, invocation, next, sb);
            else
                throw new LexicalException(error, invocation, next, sb, i.Value);
        }
        return true;
    }

    void ValidIdentifier(char value, string invocation, int? i)
    {
        // if (IsCommunicator(Value)) //unfortunately, if you mess up your identifier and insert a ( ) or , prematurely, i can know at compile time if that termination was intentional or a mistake
        //     throw new LexicalException("Detected communicator in identifier, this is illegal.", invocation, sb); //but i can know for the other communicators

        if (Illegal(value))
        {
            const string error = "Detected illegal character in identifier.";
            if (i == null)
                throw new LexicalException(error, invocation, value, sb);
            else
                throw new LexicalException(error, invocation, value, sb, i.Value);
        }
    }
    static bool Illegal(char val) => val != VaidNonAlphaNumeric && !IsLetter(val) && !IsDigit(val);
    static bool Termination(char value) => value == ParamTerminate || value == MethodTerminate;
    static bool ValidIdentifierFirstChar(char value) => value == VaidNonAlphaNumeric || value == ValidLeadingNonAlphaNumeric || IsLetter(value);
    static bool IsLetter(char value) =>
    value switch
    {
        >= 'a' and <= 'z' or >= 'A' and <= 'Z' => true,
        _ => false
    };

    static bool IsDigit(char value) =>
    value switch
    {
        >= '0' and <= '9' => true,
        _ => false
    };
    void Clear()
    {
        Main = null;
        Start = true;
        BeganFirstRead = false;
        CurrentMethod = null;
        ReadingDigit = false;
        ReadFloat = false;
        ReadingString = false;
        EndString = false;
        ReadingIdentifier = false;
        FinishedReadChar = false;
        ReadingChar = false;
        ReadingArbitrary = false;
        MethodBegan = false;
        MethodTerminated = false;
        ReadingSubparamsOf = 0;
        LastReadingValue = 0;
        sb.Length = 0;
    }

}











