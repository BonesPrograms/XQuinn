using System;
using System.Text;
using XQuinn.Extensions;

using System.Reflection;
using XQuinn.Reflection;

using System.Collections;
using XQuinn.Runtime.NavigatorEngine;

namespace XQuinn.Runtime
{
    public sealed class NavigationFeed
    {
        readonly StringBuilder _feed = new();
        internal readonly NavigatorCore _core = new();
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
        internal NavigationFeed(bool stacktrace)
        {
            StackTrace = stacktrace;
        }

        public string ChainInstruct(params string[] args)
        {
            StringBuilder sb = new();
            sb.Append('~');
            sb.AppendMany(args, ";");
            return SafeInstruct(sb.ToString());
        }
        public string SafeInstruct(string input)
        {
            _feed.Length = 0;
            string output;
            try
            {
                output = UnsafeInstruct(input);
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
        internal string UnsafeInstruct(string input)
        {
            _feed.AppendLine();
            _feed.Append(DateTime.Now);
            _feed.AppendLine();
            _feed.Append($"Instruction :");
            _feed.AppendLine(input);
            ProcessReturn(_core.Instruct(input));
            AppendNavigData();
            string output = _feed.ToString();
            _feed.Length = 0;
            return output;
        }

        void ProcessReturn(object? ret)
        {
            ReferenceBinding? variable = ret as ReferenceBinding;
            if (variable != null)
            {
                _feed.Append($"Variable: ");
                _feed.AppendLine(variable.ToString());
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
                    foreach (object? obj in enumerator)
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
            if (_core._static_type != null)
            {
                _feed.Append($"Loaded Type: ");
                _feed.AppendLine(ReflectionPrinter.Print(_core._static_type, false));
                if (_core._object_type != null)
                {
                    _feed.Append($"Loaded Instance Type: ");
                    _feed.AppendLine(ReflectionPrinter.Print(_core._object_type, false));
                    if (_core._variable != null)
                    {
                        _feed.Append($"Loaded Variable: ");
                        _feed.AppendLine(_core._variable);
                    }
                }
            }
        }
    }
}
