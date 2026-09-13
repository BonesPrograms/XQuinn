using System;
using System.Text;
using XQuinn.Extensions;
using System.Collections.Generic;
using System.Reflection;
using XQuinn.Reflection;
using System.Linq;
using System.Collections;

namespace XQuinn.Runtime
{

    /// <summary>
    /// Monitor the output, activity and exceptions of a Navigator instance via strings.
    /// </summary>
    public sealed class NavigationFeed
    {
        public bool Caching
        {
            get => _navigator.Caching;
            set => _navigator.Caching = value;
        }
        readonly StringBuilder _feed = new();
        readonly StringBuilder _collectionWriter = new();
        internal NavigatorCore _navigator = new();

        public NavigationFeed()
        {
        }
        public NavigationFeed(bool caching)
        {
            Caching = caching;
        }

        public string SafeInterface(string input, out object? interpretereReturned, out bool exception)
        {
            _feed.Length = 0;
            exception = false;
            interpretereReturned = null;
            string output;
            try
            {
                output = Interface(input, out interpretereReturned);
            }
            catch (Exception ex)
            {
                _feed.Length = 0;
                _feed.CatchException(ex);
                output = _feed.ToString();
                _feed.Length = 0;
                exception = true;
            }
            return output;
        }
        string Interface(string input, out object? navigReturnValue)
        {
            navigReturnValue = null;
            _feed.AppendLine($"{Environment.NewLine}{DateTime.Now}");
            if (input.Length != 0 && input[0] == '?')
                return Question(input.Substring(1));
            _feed.AppendLine($"Invoking : {input}");
            navigReturnValue = _navigator.Interface(input);
            ProcessReturn(navigReturnValue);
            AppendNavigData();
            string output = _feed.ToString();
            _feed.Length = 0;
            return output;
        }

        void ProcessReturn(object? ret)
        {
            var c = _feed[1];
            if (ret is IEnumerable enumerable and not string)
            {
                _feed.AppendLine($"Returned: \n{_collectionWriter.AppendMany(enumerable, Environment.NewLine, true)}");
                _collectionWriter.Length = 0;
            }
            else
                _feed.AppendLine($"Returned: {ret?.ToString() ?? "null"}");
        }

        string Question(string input)
        {
            if (input.EqualsCaseless("vars") || input.EqualsCaseless("variables"))
                return GetCollection(_navigator._variables, x => $"[Key: {x.Key} :: {x.Value}]", "variables");
            if (_navigator._loadedType == null)
                return "No type loaded.";
            if (input.EqualsCaseless("props") || input.EqualsCaseless("properties"))
                return GetCollection(_navigator._props, "properties");
            if (input.EqualsCaseless("methods"))
                return GetCollection(_navigator._methods, "methods");
            if (input.EqualsCaseless("fields"))
                return GetCollection(_navigator._fields, "fields");
            string[] arr = input.Split(':'); //type;argument;name or argument;name
            if (arr.Length >= 2)
                return Search(0, arr);
            return "Invalid query.";
        }

        string Search(int startint, string[] arr)
        {
            if (arr[startint].EqualsCaseless("props") || arr[startint].EqualsCaseless("Properties") || arr[startint].EqualsCaseless("prop") || arr[startint].EqualsCaseless("props"))
                return Extract(_navigator._props, "properties", arr[startint + 1]);
            else if (arr[startint].EqualsCaseless("method") || arr[startint].EqualsCaseless("methods"))
                return Extract(_navigator._methods, "methods", arr[startint + 1]);
            else if (arr[startint].EqualsCaseless("field") || arr[startint].EqualsCaseless("fields"))
                return Extract(_navigator._fields, "fields", arr[startint + 1]);
            return "Invalid query.";
        }

        string Extract<K, V>(IEnumerable<KeyValuePair<K, V>> extract, string kind, string containing) where V : MemberInfo
        {
            extract = extract.Where(x => x.Key!.ToString()!.ContainsCaseless(containing));
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
            _feed.AppendLine($"Printing {kind}.");
            _feed.AppendMany(collection, Environment.NewLine, false, toString);
            string output = _feed.ToString();
            _feed.Length = 0;
            return output;
        }



        void AppendNavigData()
        {
            if (_navigator._loadedType != null)
            {
                _feed.AppendLine($"Type: {_navigator._loadedType}");
                if (_navigator._instance != null)
                    _feed.AppendLine($"Instance Type: {_navigator._instanceType}");
                // sb.AppendLine($"Loaded Instance Object: {_navigator._instance}");
                if (_navigator._variable != null)
                    _feed.AppendLine($"Variable: {_navigator._variable}");
            }

        }

    }
}