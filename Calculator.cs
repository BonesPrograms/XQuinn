using System;
using XQuinn.Extensions;
using System.Text;
using System.Collections.Generic;

namespace XQuinn.Private
{

    enum MathOp
    {
        _invalid,
        Add,
        Sub,
        Div,
        Mult
    }

    abstract class MathExpr
    {

    }

    sealed class MathExpr<T> : MathExpr where T : struct
    {
        public readonly T Op;
        public MathExpr(T op)
        {
            Op = op;
        }
        public override string ToString()
        {
            return Op.ToString() ?? throw new ArgumentNullException();
        }
    }
    class Calculator //Does not yet support PEMDAS or decimals
    {
        class CalculatorException : Exception
        {
            public CalculatorException(string msg, string expr) : base($"{msg}: {expr}")
            {

            }
        }
        char _value;
        bool _skip;
        bool _readDigit;
        bool _readOp;
        bool _firstDig; //This is specifically to allow the first digit to be negative. Subsequent digits can be negative witrhout a problem,
        //this bool needs to exist to allow the first digit to be negative.
        readonly StringBuilder _digit = new();
        readonly List<MathExpr> _expression = new();
        void Clear()
        {
            _digit.Length = 0;
            _expression.Clear();
            _value = default;
            _skip = false;
            _readDigit = false;
            _firstDig = true;
            _readOp = false;
        }

        bool ValidOp(out MathOp op)
        {
            op = GetOp();
            return op != MathOp._invalid;
        }
        MathOp GetOp() =>
        _value switch
        {
            '+' => MathOp.Add,
            '-' => MathOp.Sub,
            '/' => MathOp.Div,
            '*' => MathOp.Mult,
            _ => MathOp._invalid
        };

        static int Execute(int lefthand, int righthand, MathOp op)
        {
            checked
            {
                return op switch
                {
                    MathOp.Add => lefthand + righthand,
                    MathOp.Div => lefthand / righthand,
                    MathOp.Mult => lefthand * righthand,
                    MathOp.Sub => lefthand - righthand,
                    _ => throw new InvalidOperationException()
                };
            }
        }
        public int Calculate(string expr)
        {
            Clear();
            Convert(expr);
            int length = _expression.Count;
            if (length < 3)
                throw new CalculatorException("Incomplete equation", expr);
            MathExpr<int> firstNum = (MathExpr<int>)_expression[0];
            int num = firstNum.Op;
            MathExpr<int>? lefthand = null;
            MathExpr<MathOp>? operation = null;
            for (int i = 1; i < length; i++)
            {
                if (operation != null && lefthand != null)
                {
                    num = Execute(num, lefthand.Op, operation.Op);
                    lefthand = null;
                    operation = null;
                }
                MathExpr math = _expression[i];
                if (math is MathExpr<int> value)
                    lefthand = value;
                else if (math is MathExpr<MathOp> op)
                    operation = op;
            }
            MathExpr<int> lastnum = (MathExpr<int>)_expression[length - 1]; //loop exits before last num can execute
            MathExpr<MathOp> lastop = (MathExpr<MathOp>)_expression[length - 2]; //i think these would actually be lefthand and operation and id
            num = Execute(num, lastnum.Op, lastop.Op);                              //already have themw?
            
            return num;
        }
        void Convert(string expr)
        {
            for (int i = 0; i < expr.Length; i++)
            {
                _value = expr[i];
                if (_value == ' ')
                {
                    _skip = true;
                    continue;
                }
                if (char.IsDigit(_value)|| (_value == '-' && (_readOp || _firstDig)))
                {
                    if (_skip && _readDigit)
                        throw new CalculatorException("Invalid digit", expr);
                    _digit.Append(_value);
                    _readDigit = true;
                    _firstDig = false;
                    _skip = false;
                    _readOp = false;
                }
                else if (ValidOp(out MathOp op))
                {
                    if (!_readDigit)
                        throw new CalculatorException("Empty op", expr);
                    _skip = false;
                    _readDigit = false;
                    _readOp = true;
                    int num = int.Parse(_digit.ToString());
                    _digit.Length = 0;
                    _expression.Add(new MathExpr<int>(num));
                    _expression.Add(new MathExpr<MathOp>(op));
                }
                else
                    throw new CalculatorException("Invalid op or digit", expr);
            }
            if (_readOp)
                throw new CalculatorException("Empty op", expr);
            if (_readDigit)
            {
                int num = int.Parse(_digit.ToString());
                _expression.Add(new MathExpr<int>(num));
            }

        }




    }
}