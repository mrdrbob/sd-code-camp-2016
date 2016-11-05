using System;
using System.Collections.Generic;

namespace ParsingDemo.Iteration1
{
    public interface IParser<TValue>
    {
        bool TryParse(string raw, out TValue value);
    }

    public class StringParser : IParser<string>
    {
        public bool TryParse(string raw, out string value)
        {
            value = null;

            int x = 0;
            if (x == raw.Length || raw[x] != '"')
                return false;

            x += 1;

            List<char> buffer = new List<char>();
            while (x < raw.Length && raw[x] != '"')
            {
                if (raw[x] == '\\')
                {
                    x += 1;
                    if (x == raw.Length)
                        return false;

                    if (raw[x] == '\\')
                        buffer.Add(raw[x]);
                    else if (raw[x] == '"')
                        buffer.Add(raw[x]);
                    else
                        return false;
                }
                else
                {
                    buffer.Add(raw[x]);
                }

                x += 1;
            }

            if (x == raw.Length)
                return false;

            x += 1;
            value = new string(buffer.ToArray());
            return true;
        }
    }
}
