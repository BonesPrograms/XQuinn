using System.Text;
using XQuinn.Extensions;
using XQuinn.Runtime.NavigatorEngine;
using System.Collections;
using System;

namespace XQuinn.Runtime
{

    public abstract class NavigatorLogger
    {
        public bool PrintToChat { get; set; } = true;
        public bool PrintEnumerableToChat { get; set; } = false;
        public abstract void ToLog(string text);
        public abstract void ToChat(string text);
        public void PrintLine() => Print(Environment.NewLine);
        public void Print<T>(T? obj, bool enumerable = false)
        {
            string text = obj.ToReflectionString();
            if (PrintToChat)
            {
                if (!enumerable || PrintEnumerableToChat)
                    ToChat(text);
            }
            ToLog(text);
            Flush();
        }
        public virtual void Flush()
        {

        }

    }
}