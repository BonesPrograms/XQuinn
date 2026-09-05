using System;
using System.Text;
using XQuinn.Extensions;
using XQuinn.LexicalAnalysis;
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

        readonly StringBuilder enumerator = new();
        internal readonly Navigator _navigator = new();

        public Monitor()
        {
        }
        public Monitor(bool caching)
        {
            Caching = caching;
        }

        public string SafeInterface(string input, out object? interpretereReturned, out bool exception)
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
                string navigData = sb.ToString();
                sb.Length = 0;
                sb.CatchException(ex);
                sb.Append(navigData);
                output = sb.ToString();
                sb.Length = 0;
                exception = true;
            }
            return output;
        }
        string Interface(string input, out object? navigReturnValue)
        {
            navigReturnValue = null;
            sb.AppendLine($"{Environment.NewLine}{DateTime.Now}");
            if (input.Length != 0 && input[0] == '?')
                return Question(input.Substring(1));
            sb.AppendLine($"Invoking : {input}");
            navigReturnValue = _navigator.Interface(input);
            ProcessReturn(navigReturnValue);
            AppendNavigData();
            string output = sb.ToString();
            sb.Length = 0;
            return output;
        }

        void ProcessReturn(object? ret)
        {
            if (ret is IEnumerable enumerable and not string)
            {
                sb.AppendLine($"Returned: \n{enumerator.AppendMany(enumerable, Environment.NewLine, enumerable is IList)}");
                enumerator.Length = 0;
            }
            else
                sb.AppendLine($"Returned: {ret?.ToString() ?? "null"}");
        }

        string Question(string input)
        {
            if (input.EqualsCaseless("vars") || input.EqualsCaseless("variables"))
                return GetCollection(_navigator._variables, x => $"[Key: {x.Key} :: {x.Value}]", "variables");
            if (_navigator._loadedType == null)
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
            sb.AppendMany(collection, Environment.NewLine, false, toString);
            string output = sb.ToString();
            sb.Length = 0;
            return output;
        }



        void AppendNavigData()
        {
            if (_navigator._loadedType != null)
            {
                sb.AppendLine($"Type: {_navigator._loadedType}");
                if (_navigator._instance != null)
                    sb.AppendLine($"Instance Type: {_navigator._instanceType}");
                // sb.AppendLine($"Loaded Instance Object: {_navigator._instance}");
                if (_navigator._variable != null)
                    sb.AppendLine($"Variable: {_navigator._variable}");
            }

        }

    }
}