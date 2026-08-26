using System;
using System.Text;
using XQuinn.Extensions;
using XQuinn.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using XQuinn.Reflection;
using XQuinn.Runtime;
using System.Linq;
using System.Collections;

namespace XQuinn.Runtime
{


    /// <summary>
    /// Monitor the output, activity and exceptions of a Navigator instance via strings.
    /// </summary>
    public sealed class NavigationMonitor
    {
        readonly StringBuilder sb = new();
        public readonly RuntimeNavigator Interp;
        public string LastInvoke => _lastinvoke;
        public string Invoking => _invoking;
        public string Returned => _ret;
        public string BeforeLoadedType => _btype;
        public string BeforeLoadedMethpd => _bmethod;
        public string BeforeInstanceType => _binstanceType;
        public string BeforeTypeCacheKey => _btypeCacheKey;
        public string BeforeVariableKey => _bvariableKey;
        public string BeforeInstance => _binstanceToString;
        public string AfterLoadedType => _atype;
        public string AfterLoadedMethod => _amethod;
        public string AfterInstanceType => _ainstanceType;
        public string AfterTypeCacheKey => _atypeCacheKey;
        public string AfterVariableKey => _avariableKey;
        public string AfterInstance => _ainstanceToString;
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

        string _binstanceToString = string.Empty;
        string _atype = string.Empty;
        string _amethod = string.Empty;
        string _ainstanceType = string.Empty;
        string _atypeCacheKey = string.Empty;
        string _avariableKey = string.Empty;

        string _ainstanceToString = string.Empty;
        //  string _exception = string.Empty;
        public NavigationMonitor(RuntimeNavigator? interp = null)
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
        string Interface(string input, out object? interpreterReturned)
        {
            interpreterReturned = null;
            sb.AppendLine();
            AppendWithBreak($"{DateTime.Now}");
            if (input.Length != 0 && input[0] == '?') return SwitchQuestion(input.Substring(1));
            AppendInterpData(true);
            _lastinvoke = AppendWithBreak($"LastInvoke: {_rawinvocation}");
            _rawinvocation = input;
            _invoking = AppendWithBreak($"Invoking : {input}");
            interpreterReturned = Interp.Interface(input);
            _ret = ProcessReturn(interpreterReturned);
            AppendInterpData(false);
            string output = sb.ToString();
            sb.Length = 0;
            return output;
        }

        string ProcessReturn(object? ret)
        {
            return (ret is IEnumerable enumerable and not string) ? AppendWithBreak($"Returned: \n{new StringBuilder().AppendMany(enumerable, Environment.NewLine)}") : AppendWithBreak($"Returned: {ret?.ToString() ?? "null"}");
        }



        string SwitchQuestion(string input)
        {

            if (input.EqualsCaseless("vars") || input.EqualsCaseless("variables")) return GetCollection(Interp._variables, x => $"[{x.Value}]", "variables");
            if (Interp.LoadedType == null) return "No type loaded.";
            if (input.EqualsCaseless("methods")) return GetCollection(Interp._methods, x => $"[Key: {x.Key} :: {ReflectionPrinter.String(x.Value)}]", "methods");
            if (input.EqualsCaseless("fields")) return GetCollection(Interp._fields, x => $"[Key: {x.Key} :: {ReflectionPrinter.String(x.Value)}]", "fields");
            string[] arr = input.Split(':');
            if (arr.Length > 1)
            {
                if (arr.Length != 2) throw new ArgumentException($"Invalid query, only supports 2 args. {input}");
                if (arr[0].EqualsCaseless("overloads") || arr[0].EqualsCaseless("overload"))
                {
                    IEnumerable<KeyValuePair<RuntimeNavigator.ResolvedOverload, MethodBase>> extract = Interp._overloads.
                     Where(x => x.Key.MethodKey.Contains(arr[1], StringComparison.OrdinalIgnoreCase));
                    return GetCollection(extract, x => $"[Key: {x.Key} :: {ReflectionPrinter.String(x.Value)}]", "overloads");
                }
                else if (arr[0].EqualsCaseless("method") || arr[0].EqualsCaseless("methods"))
                {
                    IEnumerable<KeyValuePair<string,MethodBase>> extract = Interp._methods.Where(x=> x.Key.Contains(arr[1], StringComparison.OrdinalIgnoreCase));
                    return GetCollection(extract, x => $"[Key: {x.Key} :: {ReflectionPrinter.String(x.Value)}]", $"method search: {arr[1]}");
                }
            }
            else if (input.EqualsCaseless("overloads")) return GetCollection(Interp._overloads, x => $"[Key: {x.Key} :: {ReflectionPrinter.String(x.Value)}]", "overloads");
            return "Invalid query.";
        }


        void AppendInterpData(bool before)
        {
            string timing = before ? "Last Loaded" : "Current Loaded";
            string type = AppendWithBreak($"{timing} Type: {Interp.LoadedType}");
            string method = AppendWithBreak($"{timing} Method: {Interp.LoadedMethod}");
            string instancetype = AppendWithBreak($"{timing} Instance Type: {Interp.InstanceType}");
            string instanceobject = AppendWithBreak($"{timing} Instance Object: {Interp.LoadedInstance}");
            string cachekey = AppendWithBreak($"{timing} TypeCacheKey {Interp.LoadedTypeKey}");
            string variablekey = $"{timing} VariableKey {Interp.LoadedVariable}";
            sb.Append(variablekey);
            if (before) sb.Append(Environment.NewLine);
            AssignOutputs(type, method, instancetype, cachekey, variablekey, instanceobject, before);


            void AssignOutputs(string type, string method, string instancetype, string cachekey, string variablekey, string instanceobj, bool before)
            {
                if (before)
                {
                    _btype = type;
                    _bmethod = method;
                    _binstanceType = instancetype;
                    _btypeCacheKey = cachekey;
                    _bvariableKey = variablekey;
                    _binstanceToString = instanceobj;
                }
                else
                {
                    _atype = type;
                    _amethod = method;
                    _ainstanceType = instancetype;
                    _atypeCacheKey = cachekey;
                    _avariableKey = variablekey;
                    _ainstanceToString = instanceobj;
                }
            }
        }

        string GetCollection<T>(IEnumerable<T> collection, Func<T?, string>? toString, string kind)
        {
            if (!collection.Any()) return $"No {kind} found.";
            sb.AppendLine();
            sb.AppendMany<T>(collection, Environment.NewLine, toString);
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