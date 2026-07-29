namespace XQuinn.NetConsole;

public static class ConsoleTools
{

    public static void WriteMany<T>(IEnumerable<T> objs, Func<T, string>? toString = null)
    {
        foreach (var obj in objs)
        {
            string? msg = toString?.Invoke(obj) ?? obj?.ToString();
            Console.WriteLine(msg);
        }
    }
    public static T? Choices<T>(IList<T> array, string? msg = null, Func<T, string>? toString = null, bool forwardLoop = true)
    {
        int index = 0;
        if (msg != null)
            msg += "\n";
        while (true)
        {
            Console.Clear();
            Console.WriteLine($"{msg}SELECT OPTION (ARROW KEYS) WITH ENTER KEY OR ESC TO RETURN:");
            Loop(array, index, toString, forwardLoop);
            var key = Console.ReadKey().Key;
            switch (key)
            {
                case ConsoleKey.Escape:
                    return default;
                case ConsoleKey.UpArrow:
                    index--;
                    break;
                case ConsoleKey.DownArrow:
                    index++;
                    break;
                case ConsoleKey.Enter:
                    return array[index];
            }
            if (index < 0)
                index = array.Count - 1;
            else if (index >= array.Count)
                index = 0;
        }
    }
    static void Loop<T>(IList<T> array, int index, Func<T, string>? toString = null, bool forwardLoop = true)
    {
        int num = forwardLoop ? 0 : array.Count - 1;
        for (int i = num; Check(forwardLoop, i, array.Count); Increment(forwardLoop, ref i))
        {
            string txt = GetString(array[i], toString);
            if (i == index)
            {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Black;
                txt = $"> {txt}";
            }
            else
                Console.ResetColor();
            Console.WriteLine(txt);
        }
    }

    static string GetString<T>(T? obj, Func<T, string>? toString = null)
    {
        if (toString == null)
        {
            if (obj == null)
                return "null";
            else if (obj is string strng)
                return strng;
            else if (obj!.ToString() is string tostrng)
                return tostrng;
            else
                return "null";
        }
        else if (obj != null)
            return toString(obj);
        else
            return "null";
    }
    static bool Check(bool forwardLoop, int i, int count) => forwardLoop ? i < count : i >= 0;
    static void Increment(bool forwardLoop, ref int i) { i = forwardLoop ? i + 1 : i - 1; }

}