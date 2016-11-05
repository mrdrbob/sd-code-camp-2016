using System.Collections.Generic;

namespace ParsingDemo.Iteration10
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

	public class IntegerParser : IParser<int>
	{
		public bool TryParse(string raw, out int value)
		{
			value = 0;

			int x = 0;
			List<char> buffer = new List<char>();
			while (x < raw.Length && char.IsDigit(raw[x]))
			{
				buffer.Add(raw[x]);
				x += 1;
			}

			if (x == 0)
				return false;

			// Deal with it.
			value = int.Parse(new string(buffer.ToArray()));
			return true;
		}
	}

	public static class Test
	{
		public static object Parse(string raw)
		{
			string valueAsString;
			if (new StringParser().TryParse(raw, out valueAsString))
				return valueAsString;

			int valueAsInteger;
			if (new IntegerParser().TryParse(raw, out valueAsInteger))
				return valueAsInteger;

			return null;
		}
	}
}