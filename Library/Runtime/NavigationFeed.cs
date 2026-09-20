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
        Type? _lastLoadedType;
        string? _lastloadedString;
        Type? _lastinstanceType;
        string? _lastInstanceString;
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

            if (ret is VariableBinding variable)
            {
                _feed.AppendLine($"Variable: {variable}");
                ret = variable.Object;
            }
            _feed.AppendLine("Returned: ");
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
                    _feed.AppendLine("Enumerable is empty.");
                else
                {
                    _feed.AppendMany(enumerable, Environment.NewLine, true, x => x is MemberInfo inf ? ReflectionPrinter.Print(inf, false) : x?.ToString());
                    _feed.AppendLine();
                }
            }
            else
                _feed.AppendLine(ret is MemberInfo inf ? ReflectionPrinter.Print(inf, false) : ret?.ToString() ?? "null");

        }


        void AppendNavigData()
        {
            if (_core._loadedType != null)
            {
                if (_lastLoadedType != _core._loadedType)
                {
                    _lastLoadedType = _core._loadedType;
                    _lastloadedString = ReflectionPrinter.Print(_core._loadedType, false);
                }
                _feed.AppendLine($"Loaded Type: {_lastloadedString}");
                if (_core._instanceType != null)
                {
                    if (_lastinstanceType != _core._instanceType)
                    {
                        _lastinstanceType = _core._instanceType;
                        _lastInstanceString = ReflectionPrinter.Print(_core._instanceType, false);
                    }
                    _feed.AppendLine($"Loaded Instance Type: {_lastInstanceString}");
                }
                else
                {
                    _lastinstanceType = null;
                    _lastInstanceString = null;
                }
                // sb.AppendLine($"Loaded Instance Object: {_navigator._instance}");
                if (_core._variable != null)
                    _feed.AppendLine($"Loaded Variable: {_core._variable}");
            }
            else
            {
                _lastLoadedType = null;
                _lastloadedString = null;
                _lastinstanceType = null;
                _lastInstanceString = null;
            }

        }

    }
}