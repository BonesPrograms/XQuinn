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
    public sealed class Monitor
    {
        public bool Caching
        {
            get => _navigator.Caching;
            set => _navigator.Caching = value;
        }
        readonly StringBuilder sb = new();
        internal readonly Navigator _navigator = new();
       // public string LastInvoke => _lastinvoke;
        public string Invoking => _invoking;
        public string Returned => _ret;
        // public string BeforeLoadedType => _btype;
        // public string BeforeLoadedMethpd => _bmethod;
        // public string BeforeInstanceType => _binstanceType;
        // public string BeforeTypeCacheKey => _btypeCacheKey;
        // public string BeforeVariableKey => _bvariableKey;
        // public string BeforeInstance => _binstanceToString;
        public string AfterLoadedType => _atype;
        public string AfterInstanceType => _ainstanceType;
        public string AfterTypeCacheKey => _atypeCacheKey;
        public string AfterVariableKey => _avariableKey;
        public string AfterInstance => _ainstanceToString;
        //    public string Exception => _exception;
       // string _rawinvocation = string.Empty;
       // string _lastinvoke = string.Empty;
        string _invoking = string.Empty;
        string _ret = string.Empty;
        // string _btype = string.Empty;
        // string _bmethod = string.Empty;
        // string _binstanceType = string.Empty;
        // string _btypeCacheKey = string.Empty;
        // string _bvariableKey = string.Empty;

        // string _binstanceToString = string.Empty;
        string _atype = string.Empty;
        string _ainstanceType = string.Empty;
        string _atypeCacheKey = string.Empty;
        string _avariableKey = string.Empty;

        string _ainstanceToString = string.Empty;
        //  string _exception = string.Empty;

        public Monitor()
        {
        }
        public Monitor(bool caching)
        {
            Caching = caching;
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
            if (input.Length != 0 && input[0] == '?')
                return SwitchQuestion(input.Substring(1));
           // AppendInterpData(true);
            //_lastinvoke = AppendWithBreak($"LastInvoke: {_rawinvocation}");
           // _rawinvocation = input;
            _invoking = AppendWithBreak($"Invoking : {input}");
            interpreterReturned = _navigator.Interface(input);
            _ret = ProcessReturn(interpreterReturned);
            AppendInterpData();
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
            if (input.EqualsCaseless("vars") || input.EqualsCaseless("variables"))
                return GetCollection(_navigator._variables, x => $"[Key: {x.Key} :: {x.Value}]", "variables");
            if (_navigator.LoadedType == null)
                return "No type loaded.";
            if (input.EqualsCaseless("methods"))
                return GetCollection(_navigator._methods, "methods");
            if (input.EqualsCaseless("fields"))
                return GetCollection(_navigator._fields, "fields");
            if (input.EqualsCaseless("overloads"))
                return GetCollection(_navigator._overloads, "overloads");
            string[] arr = input.Split(':'); //type;argument;name or argument;name
            if (arr.Length >= 2)
                return Search(0, arr);
            return "Invalid query.";
        }

        string Search(int startint, string[] arr)
        {
            if (arr[startint].EqualsCaseless("overloads") || arr[startint].EqualsCaseless("overload"))
            {
                IEnumerable<KeyValuePair<string, MethodBase>> asStrings = _navigator._overloads.Select(x => new KeyValuePair<string, MethodBase>(x.Key.ToString(), x.Value));
                return Extract(asStrings, "overloads", arr[startint + 1]);
            }
            else if (arr[startint].EqualsCaseless("method") || arr[startint].EqualsCaseless("methods"))
                return Extract(_navigator._methods, "methods", arr[startint + 1]);
            else if (arr[startint].EqualsCaseless("field") || arr[startint].EqualsCaseless("fields"))
                return Extract(_navigator._fields, "fields", arr[startint + 1]);
            return "Invalid query.";
        }

        string Extract<T>(IEnumerable<KeyValuePair<string, T>> extract, string kind, string containing) where T : MemberInfo
        {
            extract = extract.Where(x => x.Key.Contains(containing, StringComparison.OrdinalIgnoreCase));
            return GetCollection(extract, kind, containing);

        }

        string GetCollection<K, V>(IEnumerable<KeyValuePair<K, V>> col, string kind, string? containing = null) where V : MemberInfo
        {
            if (containing != null)
                kind = $"{kind} containing string {containing}";
            return GetCollection(col, x => $"[Key: {x.Key} :: {ReflectionPrinter.Print(x.Value)}]", kind);
        }

        string GetCollection<T>(IEnumerable<T> collection, Func<T?, string>? toString, string kind)
        {
            if (!collection.Any())
                return $"No {kind} found.";
            sb.AppendLine($"Printing {kind}.");
            sb.AppendMany<T>(collection, Environment.NewLine, toString);
            string output = sb.ToString();
            sb.Length = 0;
            return output;
        }



        void AppendInterpData()
        {
            string timing = "Loaded";
            string type = AppendWithBreak($"{timing} Type: {_navigator.LoadedType}");
          //  string method = AppendWithBreak($"{timing} Method: {Navig.LoadedMethod}");
            string instancetype = AppendWithBreak($"{timing} Instance Type: {_navigator.InstanceType}");
            string instanceobject = AppendWithBreak($"{timing} Instance Object: {_navigator.LoadedInstance}");
            string cachekey = AppendWithBreak($"{timing} TypeCacheKey {_navigator.LoadedTypeKey}");
            string variablekey = $"{timing} VariableKey {_navigator.LoadedVariable}";
            sb.Append(variablekey);
            //if (before)
                sb.Append(Environment.NewLine);
            AssignOutputs(type, instancetype, cachekey, variablekey, instanceobject);//before);


            void AssignOutputs(string type, string instancetype, string cachekey, string variablekey, string instanceobj)//, bool before)
            {
                // if (before)
                // {
                //     // _btype = type;
                //     // _bmethod = method;
                //     // _binstanceType = instancetype;
                //     // _btypeCacheKey = cachekey;
                //     // _bvariableKey = variablekey;
                //     // _binstanceToString = instanceobj;
                // }
                // else
                // {
                    _atype = type;
                    _ainstanceType = instancetype;
                    _atypeCacheKey = cachekey;
                    _avariableKey = variablekey;
                    _ainstanceToString = instanceobj;
               // }
            }
        }

        string AppendWithBreak(string strng)
        {
            sb.AppendLine(strng);
            return strng;
        }
    }
}