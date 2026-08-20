using System;
using System.Text;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using XQuinn.Reflection;

namespace XQuinn
{


    /// <summary>
    /// Monitor the output, activity and exceptions of a CallInterpreter instance via strings.
    /// </summary>
    public sealed class InterpMonitor
    {
        readonly StringBuilder sb = new();
        public readonly CallInterpreter Interp;
        public string LastInvoke => _lastinvoke;
        public string Invoking => _invoking;
        public string Returned => _ret;
        public string BeforeLoadedType => _btype;
        public string BeforeLoadedMethpd => _bmethod;
        public string BeforeInstanceType => _binstanceType;
        public string BeforeTypeCacheKey => _btypeCacheKey;
        public string BeforeVariableKey => _bvariableKey;
        public string AfterLoadedType => _atype;
        public string AfterLoadedMethod => _amethod;
        public string AfterInstanceType => _ainstanceType;
        public string AfterTypeCacheKey => _atypeCacheKey;
        public string AfterVariableKey => _avariableKey;
        //    public string Exception => _exception;
        string _rawinvocation = string.Empty;
        string _lastinvoke = string.Empty;
        string _invoking = string.Empty;
        string _ret = string.Empty;
        string _btype = string.Empty;
        string _bmethod = string.Empty;
        string _binstanceType = string.Empty;
        string _btypeCacheKey = string.Empty;
        string _bvariableKey = string.Empty;
        string _atype = string.Empty;
        string _amethod = string.Empty;
        string _ainstanceType = string.Empty;
        string _atypeCacheKey = string.Empty;
        string _avariableKey = string.Empty;
        //  string _exception = string.Empty;
        public InterpMonitor(CallInterpreter? interp = null)
        {
            Interp = interp ?? new();
        }

        public string TryCatchInterface(string input, out object? interpretereReturned, out bool exception)
        {
            sb.Length = 0;
            exception = false;
            interpretereReturned = null;
            string output;
            try
            {
                output = Interface(input, out interpretereReturned);
            }
            catch (Exception ex)
            {
                sb.CatchException(ex);
                output = sb.ToString();
                sb.Length = 0;
                exception = true;
            }
            return output;
        }
        public string Interface(string input, out object? interpreterReturned)
        {
            interpreterReturned = null;
            AppendWithBreak($"{DateTime.Now}");
            if (input.Length != 0 && input[0] == '?') return SwitchQuestion(input.Substring(1));
            AppendInterpData(true);
            _lastinvoke = AppendWithBreak($"LastInvoke: {_rawinvocation}");
            _rawinvocation = input;
            _invoking = AppendWithBreak($"Invoking : {input}");
            interpreterReturned = Interp.Interface(input);
            _ret = AppendWithBreak($"Returned: {interpreterReturned?.ToString()}");
            AppendInterpData(false);
            string output = sb.ToString();
            sb.Length = 0;
            return output;
        }

        string SwitchQuestion(string input) => input switch
        {
            "vars" => GetCollection(Interp._variables, null),
            "methods" => GetCollection(Interp._methods, x => $"[Key: {x.Key} :: {ReflectionPrinter.String(x.Value)}]"),
            "fields" => GetCollection(Interp._fields, x => $"[Key: {x.Key} :: {ReflectionPrinter.String(x.Value)}]"),
            "overloads" => GetCollection(Interp._overloads, x => $"[Key: {x.Key} :: {ReflectionPrinter.String(x.Value)}]"),
            _ => string.Empty
        };


        void AppendInterpData(bool before)
        {
            string timing = before ? "Last Loaded" : "Current Loaded";
            string type = AppendWithBreak($"{timing} Type: {Interp.LoadedType}");
            string method = AppendWithBreak($"{timing} Method: {Interp.LoadedMethod}");
            string instancetype = AppendWithBreak($"{timing} Instance Type: {Interp.InstanceType}");
            string cachekey = AppendWithBreak($"{timing} TypeCacheKey {Interp.LoadedTypeKey}");
            string variablekey = $"{timing} VariableKey {Interp.LoadedVariable}";
            sb.Append(variablekey);
            if (before) sb.Append(Environment.NewLine);
            AssignOutputs(type, method, instancetype, cachekey, variablekey, before);


            void AssignOutputs(string type, string method, string instancetype, string cachekey, string variablekey, bool before)
            {
                if (before)
                {
                    _btype = type;
                    _bmethod = method;
                    _binstanceType = instancetype;
                    _btypeCacheKey = cachekey;
                    _bvariableKey = variablekey;
                }
                else
                {
                    _atype = type;
                    _amethod = method;
                    _ainstanceType = instancetype;
                    _atypeCacheKey = cachekey;
                    _avariableKey = variablekey;
                }
            }
        }

        string GetCollection<T>(IEnumerable<T> collection, Func<T?, string>? toString)
        {
            sb.AppendMany(collection, Environment.NewLine, toString);
            string output = sb.ToString();
            sb.Length = 0;
            return output;
        }


        string AppendWithBreak(string strng)
        {
            sb.AppendLine(strng);
            return strng;
        }
    }
}