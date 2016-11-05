using System.Collections.Generic;

namespace ParsingDemo.Iteration21
{
	public interface ISource<Token>
	{
		Token Current { get; }
		bool HasMore { get; }
		ISource<Token> Next();
	}

	public class Result<Token, TValue>
	{
		public bool Success { get; }
		public TValue Value { get; }
		public string Message { get; }
		public ISource<Token> Next { get; }

		public Result(bool success, TValue value, string message, ISource<Token> next)
		{
			Success = success;
			Value = value;
			Message = message;
			Next = next;
		}
	}

	public interface IRule<Token, TValue>
	{
		Result<Token, TValue> TryParse(ISource<Token> source);
	}

	public class StringSource : ISource<char>
	{
		readonly string value;
		int index;

		public StringSource(string value, int index = 0) { this.value = value; this.index = index; }

		public char Current => value[index];
		public bool HasMore => index < value.Length;
		public ISource<char> Next() => new StringSource(value, index + 1);
	}

	public abstract class CharMatches : IRule<char, char>
	{
		protected abstract bool IsCharMatch(char c);

		public Result<char, char> TryParse(ISource<char> source)
		{
			if (!source.HasMore)
				return new Result<char, char>(false, '\0', "Unexpected EOF", null);
			if (!IsCharMatch(source.Current))
				return new Result<char, char>(false, '\0', $"Unexpected char: {source.Current}", null);
			return new Result<char, char>(true, source.Current, null, source.Next());
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

		public Result<Token, TResult[]> TryParse(ISource<Token> source)
		{
			List<TResult> buffer = new List<TResult>();
			while (source.HasMore)
			{
				var result = rule.TryParse(source);
				if (!result.Success)
					break;

				buffer.Add(result.Value);
				source = result.Next;
			}

			if (requireAtLeastOne && buffer.Count == 0)
			{
				return new Result<Token, TResult[]>(false, null, "Expected at least one match", null);
			}

			return new Result<Token, TResult[]>(true, buffer.ToArray(), null, source);
		}
	}

	public class FirstMatch<Token, TResult> : IRule<Token, TResult>
	{
		readonly IRule<Token, TResult>[] rules;
		public FirstMatch(IRule<Token, TResult>[] rules) { this.rules = rules; }

		public Result<Token, TResult> TryParse(ISource<Token> source)
		{
			foreach (var rule in rules)
			{
				var result = rule.TryParse(source);
				if (result.Success)
					return result;
			}

			return new Result<Token, TResult>(false, default(TResult), "No rule matched", null);
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

		public Result<Token, TOut> TryParse(ISource<Token> source)
		{
			var leftResult = leftRule.TryParse(source);
			if (!leftResult.Success)
				return new Result<Token, TOut>(false, default(TOut), leftResult.Message, null);

			var rightResult = rightRule.TryParse(leftResult.Next);
			if (!rightResult.Success)
				return new Result<Token, TOut>(false, default(TOut), rightResult.Message, null);

			var result = Combine(leftResult.Value, rightResult.Value);
			return new Result<Token, TOut>(true, result, null, rightResult.Next);
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

		public Result<Token, Token> TryParse(ISource<Token> source)
		{
			if (!source.HasMore)
				return new Result<Token, Token>(false, default(Token), "Unexpected EOF", null);

			var result = rule.TryParse(source);
			if (result.Success)
				return new Result<Token, Token>(false, default(Token), "Unexpected match", null);

			return new Result<Token, Token>(true, source.Current, null, source.Next());
		}
	}

	public abstract class MapTo<Token, TIn, TOut> : IRule<Token, TOut>
	{
		readonly IRule<Token, TIn> rule;
		protected MapTo(IRule<Token, TIn> rule) { this.rule = rule; }

		protected abstract TOut Convert(TIn value);

		public Result<Token, TOut> TryParse(ISource<Token> source)
		{
			var result = rule.TryParse(source);
			if (!result.Success)
				return new Result<Token, TOut>(false, default(TOut), result.Message, null);

			return new Result<Token, TOut>(true, Convert(result.Value), null, result.Next);
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

			var asQuote = fullQuote.TryParse(source);
			if (asQuote.Success)
				return asQuote.Value;

			var asInteger = digitsAsInt.TryParse(source);
			if (asInteger.Success)
				return asInteger.Value;

			return null;
		}
	}
}