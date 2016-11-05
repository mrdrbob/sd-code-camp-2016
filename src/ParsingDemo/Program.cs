using System;
using System.Diagnostics;

namespace ParsingDemo
{
	class Program
    {
        static void Main(string[] args)
        {
            ExecuteTest(Iteration10.Test.Parse);
            ExecuteTest(Iteration20.Test.Parse);
            ExecuteTest(Iteration21.Test.Parse);
			ExecuteTest(Iteration22.Test.Parse);
			ExecuteTest(Iteration30.Test.Parse);

			Console.WriteLine("ALL OK");
        }

        static void ExecuteTest(Func<string, object> parseFunction)
        {
            object t = parseFunction("\"test\"");
            Debug.Assert((string)parseFunction("\"test\"") == "test");
			t = parseFunction(@"""test \"" \\ test""");
			Debug.Assert((string)parseFunction(@"""test \"" \\ test""") == "test \" \\ test");
            Debug.Assert(parseFunction("\"test") == null);
            Debug.Assert(parseFunction("test\"") == null);

            Debug.Assert((int)parseFunction("5452") == 5452);
            Debug.Assert(parseFunction("a5452") == null);
        }
    }
}
