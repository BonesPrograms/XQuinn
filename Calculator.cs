using System;
using XQuinn.Extensions;
using System.Text;
using System.Collections.Generic;

namespace XQuinn.Private
{

    enum Op
    {
        _invalid,
        Add,
        Sub,
        Div,
        Mult
    }

    class Operation
    {

    }

    class Operation<T> : Operation where T : struct
    {
        public readonly T Op;

        public Operation(T op)
        {
            Op = op;
        }

        public override string ToString()
        {
            return Op!.ToString()!;
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

        readonly StringBuilder _digit = new();
        readonly List<Operation> _ops = new();
        void Clear()
        {
            _digit.Length = 0;
            _ops.Clear();
            _value = default;
            _skip = false;
            _readDigit = false;
            _readOp = false;
        }

        bool ValidOp(out Op op)
        {
            op = GetOp();
            return op != Op._invalid;
        }
        Op GetOp() =>
        _value switch
        {
            '+' => Op.Add,
            '-' => Op.Sub,
            '/' => Op.Div,
            '*' => Op.Mult,
            _ => Op._invalid
        };

        public int Calculate(string expr)
        {
            Clear();
            Convert(expr);
            int length = _ops.Count;
            if (length < 3)
                throw new CalculatorException("Incomplete equation ", expr);
            Operation<int> firstOp = (Operation<int>)_ops[0];
            int num = firstOp.Op;
            Operation<int>? lefthand = null;
            Operation<Op>? operation = null;
            for (int i = 1; i < length; i++)
            {
                Operation op = _ops[i];
                if (operation != null && lefthand != null)
                {
                    num = Execute(num, lefthand.Op, operation.Op);
                    lefthand = null;
                    operation = null;
                }
                if (op is Operation<int> value)
                    lefthand ??= value;
                if (op is Operation<Op> math)
                    operation = math;

            }
            Operation<int> lastnum = (Operation<int>)_ops[length - 1];
            Operation<Op> lastop = (Operation<Op>)_ops[length - 2];
            num = Execute(num, lastnum.Op, lastop.Op);
            return num;
        }

        static int Execute(int lefthand, int righthand, Op op) =>
        op switch
        {
            Op.Add => lefthand + righthand,
            Op.Div => lefthand / righthand,
            Op.Mult => lefthand * righthand,
            Op.Sub => lefthand - righthand,
            _ => throw new InvalidOperationException()
        };
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
                if (_value.IsDigit())
                {
                    if (_skip && _readDigit)
                        throw new CalculatorException("Invalid digit", expr);
                    _digit.Append(_value);
                    _readDigit = true;
                    _skip = false;
                    _readOp = false;
                }
                else if (ValidOp(out Op op))
                {
                    if (!_readDigit || _readOp)
                        throw new CalculatorException("Empty op", expr);
                    _skip = false;
                    _readDigit = false;
                    _readOp = true;
                    int num = int.Parse(_digit.ToString());
                    _digit.Length = 0;
                    _ops.Add(new Operation<int>(num));
                    _ops.Add(new Operation<Op>(op));
                }
                else
                    throw new CalculatorException("Invalid op", expr);
            }
            if (_readOp)
                throw new CalculatorException("Empty op", expr);
            if (_readDigit)
            {
                int num = int.Parse(_digit.ToString());
                _ops.Add(new Operation<int>(num));
            }

        }




    }
}