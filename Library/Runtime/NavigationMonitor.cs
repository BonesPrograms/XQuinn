using System;
using System.Text;
using XQ.Extensions;
using XQ.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using XQ.Reflection;
using XQ.Runtime;
using System.Linq;
using System.Collections;

namespace XQ.Runtime
{


    /// <summary>
    /// Monitor the output, activity and exceptions of a Navigator instance via strings.
    /// </summary>
    public sealed class NavigationMonitor
    {
        public bool Caching
        {
            get => Navig.Caching;
            set => Navig.Caching = value;
        }
        readonly StringBuilder sb = new();
        internal readonly Navigator Navig = new();
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

        public NavigationMonitor()
        {
        }
        public NavigationMonitor(bool caching)
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
            AppendInterpData(true);
            _lastinvoke = AppendWithBreak($"LastInvoke: {_rawinvocation}");
            _rawinvocation = input;
            _invoking = AppendWithBreak($"Invoking : {input}");
            interpreterReturned = Navig.Interface(input);
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
            if (input.EqualsCaseless("vars") || input.EqualsCaseless("variables"))
                return GetCollection(Navig._variables, x => $"[Key: {x.Key} :: {x.Value}]", "variables");
            if (Navig.LoadedType == null)
                return "No type loaded.";
            if (input.EqualsCaseless("methods"))
                return GetCollection(Navig._methods, "methods");
            if (input.EqualsCaseless("fields"))
                return GetCollection(Navig._fields, "fields");
            if (input.EqualsCaseless("overloads"))
                return GetCollection(Navig._overloads, "overloads");
            string[] arr = input.Split(':'); //type;argument;name or argument;name
            if (arr.Length >= 2)
                return Search(0, arr);
            return "Invalid query.";
        }

        string Search(int startint, string[] arr)
        {
            if (arr[startint].EqualsCaseless("overloads") || arr[startint].EqualsCaseless("overload"))
            {
                IEnumerable<KeyValuePair<string, MethodBase>> asStrings = Navig._overloads.Select(x => new KeyValuePair<string, MethodBase>(x.ToString(), x.Value));
                return Extract(asStrings, "overloads", arr[startint + 1]);
            }
            else if (arr[startint].EqualsCaseless("method") || arr[startint].EqualsCaseless("methods"))
                return Extract(Navig._methods, "methods", arr[startint + 1]);
            else if (arr[startint].EqualsCaseless("field") || arr[startint].EqualsCaseless("fields"))
                return Extract(Navig._fields, "fields", arr[startint + 1]);
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



        void AppendInterpData(bool before)
        {
            string timing = before ? "Last Loaded" : "Current Loaded";
            string type = AppendWithBreak($"{timing} Type: {Navig.LoadedType}");
            string method = AppendWithBreak($"{timing} Method: {Navig.LoadedMethod}");
            string instancetype = AppendWithBreak($"{timing} Instance Type: {Navig.InstanceType}");
            string instanceobject = AppendWithBreak($"{timing} Instance Object: {Navig.LoadedInstance}");
            string cachekey = AppendWithBreak($"{timing} TypeCacheKey {Navig.LoadedTypeKey}");
            string variablekey = $"{timing} VariableKey {Navig.LoadedVariable}";
            sb.Append(variablekey);
            if (before)
                sb.Append(Environment.NewLine);
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

        string AppendWithBreak(string strng)
        {
            sb.AppendLine(strng);
            return strng;
        }
    }
}