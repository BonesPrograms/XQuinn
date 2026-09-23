using System;
using System.Text;
using XQuinn.Extensions;
using System.Collections.Generic;
using System.Reflection;
using XQuinn.Reflection;
using System.Linq;
using System.Collections;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.Runtime
{

    /// <summary>
    /// Monitor the output, activity and exceptions of a Navigator instance via strings.
    /// </summary>
    public sealed class NavigationFeed
    {
        readonly StringBuilder _feed = new();
        readonly internal NavigatorCore _core = new();
        public bool StackTrace
        {
            get => _stackTrace;
            set
            {
                _stackTrace = value;
                _core.StackTrace = value;
            }
        }
        bool _stackTrace;
        public NavigationFeed()
        {
        }
        public string SafeInterface(string input)
        {
            _feed.Length = 0;
            string output;
            try
            {
                output = Interface(input);
            }
            catch (Exception ex)
            {
                _feed.Length = 0;
                _feed.CatchException(ex, StackTrace);
                output = _feed.ToString();
                _feed.Length = 0;
            }
            return output;
        }
        internal string Interface(string input)
        {
            _feed.AppendLine($"{Environment.NewLine}{DateTime.Now}");
            _feed.AppendLine($"Instruction : {input}");
            ProcessReturn(_core.Interface(input));
            AppendNavigData();
            string output = _feed.ToString();
            _feed.Length = 0;
            return output;
        }

        void ProcessReturn(object? ret)
        {
            VariableBinding? variable = ret as VariableBinding;
            if (variable != null)
            {
                _feed.AppendLine($"Variable: {variable}");
                ret = variable.Object;
            }
            IEnumerable? enumerator = ret is string ? null : ret as IEnumerable;
            if (variable == null || enumerator != null)
                _feed.AppendLine("Returned: ");
            if (enumerator != null)
            {
                bool empty = true;
                if (enumerator is ICollection col)
                    empty = col.Count == 0;
                else
                    foreach (object? obj in enumerator) //Find this funny because im only counting one time but i cant use .Any so it is what it is
                    {
                        empty = false;
                        break;
                    }
                if (empty)
                    _feed.AppendLine("Enumerable is empty.");
                else
                {
                    _feed.AppendMany(enumerator, Environment.NewLine, true, x => x is MemberInfo inf ? ReflectionPrinter.Print(inf, false) : x?.ToString());
                    _feed.AppendLine();
                }
            }
            else if (variable == null)
                _feed.AppendLine(ret is MemberInfo inf ? ReflectionPrinter.Print(inf, false) : ret?.ToString() ?? "null");

        }


        void AppendNavigData()
        {
            if (_core._loadedType != null)
            {
                _feed.AppendLine($"Loaded Type: {ReflectionPrinter.Print(_core._loadedType, false)}");
                if (_core._instanceType != null)
                {
                    _feed.AppendLine($"Loaded Instance Type: {ReflectionPrinter.Print(_core._instanceType, false)}");
                    if (_core._variable != null)
                        _feed.AppendLine($"Loaded Variable: {_core._variable}");
                }

            }

        }
    }
}