#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XQuinn.Extensions;
using XQuinn.IO;
using XQuinn.IO.Finders;

namespace XQuinn.GameModding
{
    internal static class VietnamWarWishes
    {
        static string s_path = Path.Combine(CodeLabFinder.s_path, @"VietnamWarAssembly\ChatBot.cs");
        const string ExtractStart = "private void CompareChatWords()";
        const string Search = "if (Chat ==";
        const string Or = "||";
        const int LineLimit = 1725;

        public static void PrintTo(string path)
        {
            using Logger logger = Logger.New(path, true);
            logger.Log(Print());
        }
        public static string Print()
        {
            IEnumerable<string> lines = File.ReadLines(s_path);
            int i = 0;
            StringBuilder sb = new();
            bool beginExtraction = false;
            foreach (string enumeratedLine in lines)
            {
                if (i >= LineLimit)
                    break;
                ReadOnlySpan<char> line = enumeratedLine.AsSpan().Trim();
                if (beginExtraction)
                    ExtractIfWish(line, sb);
                else
                    beginExtraction = line.SequenceEqual(ExtractStart);
                i++;
            }
            return sb.ToString();
        }


        static void ExtractIfWish(ReadOnlySpan<char> line, StringBuilder sb)
        {

            if (line.Contains(Search, StringComparison.Ordinal))
            {
                if (line.Contains(Or, StringComparison.Ordinal))
                {
                    OrSort(line, sb);
                }
                else
                {
                    Append(line, sb);
                }
            }
        }

        static void Append(ReadOnlySpan<char> line, StringBuilder sb)
        {
            sb.Append(SliceQuotes(line));
            sb.AppendLine();
        }

        static void OrSort(ReadOnlySpan<char> line, StringBuilder sb)
        {
            string[] ors = line.ToString().Split(Or);
            foreach (string or in ors)
            {
                Append(or, sb);
            }
        }

        static ReadOnlySpan<char> SliceQuotes(ReadOnlySpan<char> line)
        {
            int and = line.IndexOf("&&");
            if (and >= 0)
                line = line.Slice(0, and);
            line = line.Slice(line.LastIndexOf('='));
            line = line.Slice(line.IndexOf('"') + 1, line.LastIndexOf('"') - 3);
            return line;
        }
    }
}
#endif