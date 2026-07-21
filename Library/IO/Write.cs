namespace XQuinn.IO;

public static  class Write// : IDisposable
{
    public static void SafetyCheck(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string? dir = Path.GetDirectoryName(path);
        ArgumentNullException.ThrowIfNull(dir);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(path))
        {
            using FileStream fs = File.Create(path);
        }

    }
  //  StreamWriter Writer;
//
    //public Write(string path, bool safe = true)
    // {
    //     if(safe)
    //     SafetyCheck(path);
    //     Writer = new(path);
    // }

    // public void To(string msg) => Writer.WriteLine(msg);

    // public void Dispose()=>Writer.Close();

    // public static void To<T>(string path, T? message, int count, bool append = true)
    // {
    //     using StreamWriter writer = Stream(path, append);
    //     string? msg = GetMessage(message);
    //     for (int i = 0; i < count; i++)
    //     {
    //         writer.WriteLine(msg);
    //     }
    // }
    // public static void To<T>(string path, T? message, bool append = true)
    // {
    //     using StreamWriter writer = Stream(path, append);
    //     string? msg = GetMessage(message);
    //     writer.WriteLine(msg);
    //     // writer.Close();
    // }

    // static string? GetMessage<T>(T? obj)
    // {
    //     if (obj is string strng)
    //         return strng;
    //     else if (obj?.ToString() is string tostrng)
    //         return tostrng;
    //     return null;
    // }
    // static StreamWriter Stream(string path, bool append)
    // {
    //     SafetyCheck(path);
    //     return new(path, append);
    // }
    // // public static void To<T>(string path, IList<T> objs, Func<T, string>? expr = null, bool append = true, bool writeLine = true)
    // // {
    // //     SafetyCheck(path);
    // //     using StreamWriter writer = new(path, append);
    // //     foreach (var obj in objs)
    // //     {
    // //         string? write;
    // //         if (expr != null)
    // //             write = expr(obj);
    // //         else
    // //             write = obj?.ToString();
    // //         if (writeLine)
    // //             writer.WriteLine(write);
    // //         else
    // //             writer.Write(write);
    // //     }
    // //     // writer.Close();
    // // }

    /// <summary>
    /// Logs to dump.txt;
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    // public static string LogFile(object input, string path)
    // {
    //     string text = input?.ToString() ?? "null";
    //     byte[] textbytes = System.Text.Encoding.UTF8.GetBytes($"\n{DateTime.Now} {text}");
    //     using (FileStream fs = File.Open(path, FileMode.Append))
    //     {
    //         fs.Write(textbytes, 0, textbytes.Length);
    //     }
    //     return text;
    // }


}