using System.Collections.Generic;

namespace ParsingDemo.Iteration20
{
	public interface ISource<Token>
	{
		Token Current { get; }
		bool HasMore { get; }
		int CurrentIndex { get; }
		void Move(int index);
	}

	public interface IRule<Token, TResult>
	{
		bool TryParse(ISource<Token> source, out TResult result);
	}

	public class StringSource : ISource<char>
	{
		readonly string value;
		int index;

		public StringSource(string value) { this.value = value; }

		public char Current => value[index];
		public int CurrentIndex => index;
		public bool HasMore => index < value.Length;
		public void Move(int index) => this.index = index;
	}

	public abstract class CharMatches : IRule<char, char>
	{
		protected abstract bool IsCharMatch(char c);

		public bool TryParse(ISource<char> source, out char result)
		{
			result = default(char);
			if (!source.HasMore)
				return false;
			if (!IsCharMatch(source.Current))
				return false;
			result = source.Current;
			source.Move(source.CurrentIndex + 1);
			return true;
		}
	}

	public class CharIsDigit : CharMatches
	{
		protected override bool IsCharMatch(char c) => char.IsDigit(c);
	}

	public class CharIs : CharMatches
	{
		readonly char toMatch;
		public CharIs(char toMatch) { this.toMatch = toMatch; }
		protected override bool IsCharMatch(char c) => c == toMatch;
	}

	public class Many<Token, TResult> : IRule<Token, TResult[]>
	{
		readonly IRule<Token, TResult> rule;
		readonly bool requireAtLeastOne;

		public Many(IRule<Token, TResult> rule, bool requireAtLeastOne) { this.rule = rule; this.requireAtLeastOne = requireAtLeastOne; }

		public bool TryParse(ISource<Token> source, out TResult[] results)
		{
			List<TResult> buffer = new List<TResult>();
			while (source.HasMore)
			{
				int originalIndex = source.CurrentIndex;
				TResult result;
				bool matched = rule.TryParse(source, out result);
				if (!matched)
				{
					source.Move(originalIndex);
					break;
				}

				buffer.Add(result);
			}

			if (requireAtLeastOne && buffer.Count == 0)
			{
				results = null;
				return false;
			}

			results = buffer.ToArray();
			return true;
		}
	}

	public class FirstMatch<Token, TResult> : IRule<Token, TResult>
	{
		readonly IRule<Token, TResult>[] rules;
		public FirstMatch(IRule<Token, TResult>[] rules) { this.rules = rules; }

		public bool TryParse(ISource<Token> source, out TResult result)
		{
			foreach (var rule in rules)
			{
				int originalIndex = source.CurrentIndex;
				if (rule.TryParse(source, out result))
					return true;
				source.Move(originalIndex);
			}

			result = default(TResult);
			return false;
		}
	}

	public abstract class MatchThen<Token, TLeft, TRight, TOut> : IRule<Token, TOut>
	{
		readonly IRule<Token, TLeft> leftRule;
		readonly IRule<Token, TRight> rightRule;

		public MatchThen(IRule<Token, TLeft> leftRule, IRule<Token, TRight> rightRule)
		{
			this.leftRule = leftRule;
			this.rightRule = rightRule;
		}

		protected abstract TOut Combine(TLeft leftResult, TRight rightResult);

		public bool TryParse(ISource<Token> source, out TOut result)
		{
			int originalIndex = source.CurrentIndex;
			result = default(TOut);
			TLeft leftResult;
			if (!leftRule.TryParse(source, out leftResult))
			{
				source.Move(originalIndex);
				return false;
			}

			TRight rightResult;
			if (!rightRule.TryParse(source, out rightResult))
			{
				source.Move(originalIndex);
				return false;
			}

			result = Combine(leftResult, rightResult);
			return true;
		}
	}

	public class MatchThenKeep<Token, TLeft, TRight> : MatchThen<Token, TLeft, TRight, TRight>
	{
		public MatchThenKeep(IRule<Token, TLeft> leftRule, IRule<Token, TRight> rightRule) : base(leftRule, rightRule) { }

		protected override TRight Combine(TLeft leftResult, TRight rightResult) => rightResult;
	}

	public class MatchThenIgnore<Token, TLeft, TRight> : MatchThen<Token, TLeft, TRight, TLeft>
	{
		public MatchThenIgnore(IRule<Token, TLeft> leftRule, IRule<Token, TRight> rightRule) : base(leftRule, rightRule) { }

		protected override TLeft Combine(TLeft leftResult, TRight rightResult) => leftResult;
	}

	public class Not<Token, TResult> : IRule<Token, Token>
	{
		readonly IRule<Token, TResult> rule;
		public Not(IRule<Token, TResult> rule) { this.rule = rule; }

		public bool TryParse(ISource<Token> source, out Token result)
		{
			result = default(Token);
			if (!source.HasMore)
				return false;

			int originalIndex = source.CurrentIndex;
			TResult throwAwayResult;
			bool matches = rule.TryParse(source, out throwAwayResult);
			if (matches)
			{
				source.Move(originalIndex);
				return false;
			}

			source.Move(originalIndex);
			result = source.Current;
			source.Move(originalIndex + 1);
			return true;
		}
	}

	public abstract class MapTo<Token, TIn, TOut> : IRule<Token, TOut>
	{
		readonly IRule<Token, TIn> rule;
		protected MapTo(IRule<Token, TIn> rule) { this.rule = rule; }

		protected abstract TOut Convert(TIn value);

		public bool TryParse(ISource<Token> source, out TOut result)
		{
			result = default(TOut);

			int originalIndex = source.CurrentIndex;
			TIn resultIn;
			if (!rule.TryParse(source, out resultIn))
			{
				source.Move(originalIndex);
				return false;
			}

			result = Convert(resultIn);
			return true;
		}
	}

	public class JoinText : MapTo<char, char[], string>
	{
		public JoinText(IRule<char, char[]> rule) : base(rule) { }

		protected override string Convert(char[] value) => new string(value);
	}

	public class MapToInteger : MapTo<char, string, int>
	{
		public MapToInteger(IRule<char, string> rule) : base(rule) { }

		protected override int Convert(string value) => int.Parse(value);
	}

	public static class Test
	{
		public static object Parse(string raw)
		{
			var quote = new CharIs('"');
			var slash = new CharIs('\\');
			var escapedQuote = new MatchThenKeep<char, char, char>(slash, quote);
			var escapedSlash = new MatchThenKeep<char, char, char>(slash, slash);
			var notQuote = new Not<char, char>(quote);

			var insideQuoteChar = new FirstMatch<char, char>(new[] {
				(IRule<char, char>)escapedQuote,
				escapedSlash,
				notQuote
			});

			var insideQuote = new Many<char, char>(insideQuoteChar, false);

			var insideQuoteAsString = new JoinText(insideQuote);
			var openQuote = new MatchThenKeep<char, char, string>(quote, insideQuoteAsString);
			var fullQuote = new MatchThenIgnore<char, string, char>(openQuote, quote);

			var digit = new CharIsDigit();
			var digits = new Many<char, char>(digit, true);
			var digitsString = new JoinText(digits);
			var digitsAsInt = new MapToInteger(digitsString);

			var source = new StringSource(raw);

			string asQuote;
			if (fullQuote.TryParse(source, out asQuote))
				return asQuote;

			source.Move(0);
			int asInteger;
			if (digitsAsInt.TryParse(source, out asInteger))
				return asInteger;

			return null;
		}
	}
}