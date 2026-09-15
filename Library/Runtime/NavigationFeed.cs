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
            get => _core.Caching;
            set => _core.Caching = value;
        }
        readonly StringBuilder _feed = new();
        readonly StringBuilder _collectionWriter = new();
        readonly internal NavigatorCore _core = new();

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
                output = Interface(input, out interpretereReturned, out exception);
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
        internal string Interface(string input, out object? navigReturnValue, out bool chainexception)
        {
            chainexception = false;
            navigReturnValue = null;
            _feed.AppendLine($"{Environment.NewLine}{DateTime.Now}");
            _feed.AppendLine($"Invoking : {input}");
            navigReturnValue = _core.Interface(input, out chainexception);
            ProcessReturn(navigReturnValue);
            AppendNavigData();
            string output = _feed.ToString();
            _feed.Length = 0;
            return output;
        }

        void ProcessReturn(object? ret)
        {
            if (ret is IEnumerable enumerable and not string)
            {
                bool empty = true;
                if (enumerable is ICollection col)
                    empty = col.Count == 0;
                else
                    foreach (object? obj in enumerable) //Find this funny because im only counting one time but i cant use .Any so it is what it is
                    {
                        empty = false;
                        break;
                    }
                if (empty)
                    _feed.AppendLine($"Returned: Enumerable is empty.");
                else
                    _feed.AppendLine($"Returned: \n{_collectionWriter.AppendMany(enumerable, Environment.NewLine, true)}");
                _collectionWriter.Length = 0;
            }
            else
                _feed.AppendLine($"Returned: {ret?.ToString() ?? "null"}");
        }


        void AppendNavigData()
        {
            if (_core._loadedType != null)
            {
                _feed.AppendLine($"Type: {_core._loadedType}");
                if (_core._instance != null)
                    _feed.AppendLine($"Instance Type: {_core._instanceType}");
                // sb.AppendLine($"Loaded Instance Object: {_navigator._instance}");
                if (_core._variable != null)
                    _feed.AppendLine($"Variable: {_core._variable}");
            }

        }

    }
}