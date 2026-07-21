using System.Text;
using XQuinn.Parsing.AST;

namespace XQuinn.Parsing;

public sealed class InvocationLexer
{

    //Operators
    const char Start = '(';

    const char Terminate = ')';

    const char Param = ',';

    const char MethodInvoc = ':'; //Type:Method
    readonly StringBuilder sb = new();

    Method? Main;

    Method? CurrentMethod;

    TypeString? CurrentType;

    char? LastOp;

    bool ReadingMethodName;

    int ReadingSubparamsOf;

    int LastReadingValue; //this is used to track how deeply we are reading parameters
                          //so if readingsubparamsof == 2, we are reading a method that is the parameter of a method that is a parameter of the "main" method. ie. Method(typename:MethodTwo(typename:MethodThree())) //reading methodThree gives us a reading value of 2
                          //once were done reading (we see a Terminate op), we decrement ReadingSubParams
    int I = 0;              //and if lastreadingvalue is > readingsubparams,it lets us know "okay, we just finished reading method params, return to the method
                            //that we were reading before we started reading this one"

    char? Op;

    public Method ParameterTemplate(string invocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocation, nameof(invocation));
        Clear();
        while (I < invocation.Length)
        {
            Op = invocation[I];
            if (LastOp == null) //Method(Param1,Param2)
            {
                if (Op == Start)
                {
                    ReadMain();
                    goto Increment;
                }
                goto Append;
            }
            if (ReadingMethodName)
            {
                if(Op == Terminate || Op == Param)
                {
                    ReadFieldName();
                    goto Increment;
                }
                if (Op == Start)
                {
                    ReadMethodName();
                    goto Increment;
                }
                goto Append;
            }
            if (ReadingSubparamsOf > 0 && Op == Terminate)
            {
                ReadingSubparamsOf--;
                goto Param;

            }       //you want to reset the CurrentMethod *after* you finish reading its final parameter
            if (LastReadingValue > ReadingSubparamsOf)// && CurrentMethod!._nest != null) //not necessary because the reading counter prevents this by its logic
            {
                CurrentMethod = CurrentMethod!._paramOf;
                LastReadingValue--;
            }
            if (Op == Terminate)
            {
                if (sb.Length >= 1)
                    goto Param;
                Refresh();
                goto Increment;
            }
        Param:
            if (Op == Param || Op == Terminate)
            {
                ReadParam();
                goto Increment;

            }
            if (Op == MethodInvoc)
            {
                ReadTypeName();
                goto Increment;
            }
        Append:
            sb.Append(Op);

        Increment:
            I++;
        }
        return Main!;
    }
    void Refresh()
    {
        sb.Length = 0;
        LastOp = Op;

    }
    void ReadMain()
    {
        //reads everything prior to (
        Method method = new(sb.ToString());
        Refresh();
        CurrentMethod = method;
        Main = method;

    }

    void ReadFieldName()
    {
        TypeString type = CurrentType!;
        Field field = new(sb.ToString(), CurrentMethod, type);
        Refresh();
        CurrentMethod!.Add(field);
        ReadingMethodName = false;
        CurrentType = null;
    }
    void ReadMethodName()
    {
        TypeString type = CurrentType!;
        Method method = new(sb.ToString(), CurrentMethod, type);
        Refresh();
        CurrentMethod!.Add(method);
        CurrentMethod = method;
        ReadingMethodName = false;
        ReadingSubparamsOf++;
        LastReadingValue++;
        CurrentType = null;
    }

    void ReadParam()
    {
        string prm = sb.ToString();
        Parameter param = new(prm, CurrentMethod);
        Refresh();
        CurrentMethod!.Add(param);
    }

    void ReadTypeName()
    {
        TypeString typeName = new(sb.ToString(), CurrentMethod);
        CurrentType = typeName;
        Refresh();
        ReadingMethodName = true;

    }

    void Clear()
    {
        Main = null;
        CurrentMethod = null;
        CurrentType = null;
        LastOp = null;
        Op = null;
        ReadingMethodName = false;
        ReadingSubparamsOf = 0;
        LastReadingValue = 0;
        I = 0;
        sb.Length = 0;
    }

}











