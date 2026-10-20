using System;
using System.Collections;
using XQuinn.Runtime.NavigatorEngine;
using XQuinn.Extensions;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

namespace XQuinn.Runtime

{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Constructor, AllowMultiple = false)]
    public sealed class HideFromNavigatorAttribute : Attribute
    {

    }
    public static class Navigator
    {
        public static NavigatorLogger Logger
        {
            get => _logger == null ? throw new InvalidOperationException("Logger not initialized!") : _logger;
            set => _logger = value;
        }
        static NavigatorLogger? _logger;
        static NavigatorCore Core { get; } = new();
        static List<string> Instructions { get; } = new();
        static StringBuilder EnumerableWriter { get; } = new();
        static List<Return> Returns { get; } = new();
        public static void Clear() => Core.Clear();
        public static void Instruct(params string[] args)
        {
            StringBuilder sb = new();
            sb.AppendMany(args, "; ");
            Instruct(sb.ToString());
        }
        public static void Instruct(string instruction)
        {
            Logger.ToLog(DateTime.Now.ToString());
            Logger.ToLog($"Instruction: {instruction}");
            MiniLexer.Autochain(Instructions, instruction);
            bool exception = false;
            foreach (string cmd in Instructions)
            {
                try
                {
                    object? ret = Core.Instruct(cmd);
                    if (ret is not NavigatorCore.EmptyInstruction)
                    {
                        Return returned = new(ret, cmd);
                        Returns.Add(returned);
                    }
                }
                catch (Exception ex)
                {
                    LogReturns(true);
                    Logger.Print($"!!! EXCEPTION on [Instructinon: {cmd}]");
                    Logger.Print($"Exception Type: {ex.GetType()}");
                    Logger.Print($"Message: {ex.Message}");
                    Logger.ToLog($"StackTrace: {Environment.NewLine} {ex.StackTrace}");
                    Logger.ToLog($"Inner Exception: {ex.InnerException?.GetType().ToString() ?? "<none>"}");
                    Logger.ToLog($"Data: {ex.Data}");
                    Logger.ToLog($"Target Site: {ex.TargetSite?.ToString() ?? "<none>"}");
                    Logger.ToLog($"Source: {ex.Source ?? "<none>"}");
                    Logger.ToLog(string.Empty);
                    exception = true;
                }
            }
            if (!exception)
                LogReturns(false);
            AppendNavigData();
            Logger.PrintLine();

        }

        static void LogReturns(bool exception)
        {
            if (Returns.Count == 0 && !exception)
            {
                Logger.Print("No instructions processed.");
            }
            else if (Returns.Count == 1)
            {
                Return ret = Returns[0];
                ProcessReturn(ret.Object);
            }
            else
            {
                EnumerableWriter.Length = 0;
                EnumerableWriter.AppendMany(Returns, Environment.NewLine, true);
                Logger.Print(EnumerableWriter, true);
            }
            Returns.Clear();
        }

        static void AppendNavigData()
        {
            if (Core.StaticType != null)
            {
                Logger.Print($"Type: {Core.StaticType.ToReflectionString()}");
                if (Core.ObjectType != null)
                {
                    Logger.Print($"Object Type: {Core.ObjectType.ToReflectionString()}");
                    if (Core.Reference != null)
                        Logger.Print($"Reference: {Core.Reference}");
                }
            }
        }
        static void ProcessReturn(object? ret)
        {
            bool referenced = false;
            if (ret is Reference reference)
            {
                Logger.Print($"Reference: {reference}");
                ret = reference.Object;
                referenced = true;
            }
            IEnumerable? enumerator = ret is string ? null : ret as IEnumerable;
            if (!referenced || enumerator != null)
                Logger.Print("Returned: ");
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
                    Logger.Print("Enumerable is empty.");
                else
                {
                    EnumerableWriter.Length = 0;
                    EnumerableWriter.AppendMany(enumerator, Environment.NewLine, true);
                    Logger.Print(EnumerableWriter, true);
                }
            }
            else if (!referenced)
                Logger.Print(ret);

        }

        readonly struct Return
        {
            public readonly object? Object { get; init; }
            readonly string AsString { get; init; }
            public Return(object? obj, string cmd)
            {
                Object = obj;
                AsString = $"[Instruction: {cmd} :: Returned: {obj.ToReflectionString()}]";
            }

            public override string ToString()
            {
                return AsString;
            }
        }
    }


}
