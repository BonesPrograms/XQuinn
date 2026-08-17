using System;
using System.IO;
using System.Text;

namespace XQuinn.IO
{

    public sealed class Logger : IDisposable
    {

        readonly StringBuilder sb = new();
        public readonly StreamWriter Writer;

        //    static readonly object _lock = new();
        Logger(string path)
        {
            Writer = new(path, true)
            {
                AutoFlush = true
            };
        }
        public static Logger New(string path, bool safe)
        {
            if (safe) SafetyCheck(path);
            Logger logger = new(path);
            logger.Log("Begin Log", 4, 2);
            return logger;
        }
        public void Log(string text, int newLinesBefore = 0, int newLinesAfter = 0)
        {
            for (int i = 1; i <= newLinesBefore; i++) sb.Append(Environment.NewLine);
            sb.Append($"[{DateTime.Now}] {text}");
            for (int i = 1; i <= newLinesAfter; i++) sb.Append(Environment.NewLine);
            Writer.WriteLine(sb);
            sb.Length = 0;
        }

        public void Dispose()
        {
            Writer.Close();
            GC.SuppressFinalize(this);
        }




        public static void SafetyCheck(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Pah cannot be null or whitespace.");
            string? dir = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(dir)) throw new ArgumentException("Unable to get directory name.");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(path)) { using FileStream fs = File.Create(path); }

        }
    }

}