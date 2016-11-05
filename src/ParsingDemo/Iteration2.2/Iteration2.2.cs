using System;
using System.Collections.Generic;

namespace ParsingDemo.Iteration22
{
	public interface ISource<Token>
	{
		T Match<T>(Func<EmtySource<Token>, T> empty,
			Func<SourceWithMoreContent<Token>, T> hasMore);
	}

	public class EmtySource<Token> : ISource<Token>
	{
		// No properties!  No state!  Let's just make it singleton.
		EmtySource() { }

		public static readonly EmtySource<Token> Instance = new EmtySource<Token>();

		public T Match<T>(Func<EmtySource<Token>, T> empty,
			Func<SourceWithMoreContent<Token>, T> hasMore) => empty(this);
	}

	public class SourceWithMoreContent<Token> : ISource<Token>
	{
		readonly Func<ISource<Token>> getNext;

		public SourceWithMoreContent(Token current, Func<ISource<Token>> getNext) { Current = current; this.getNext = getNext; }

		public Token Current { get; set; }
		public ISource<Token> Next() => getNext();

		public T Match<T>(Func<EmtySource<Token>, T> empty,
			Func<SourceWithMoreContent<Token>, T> hasMore) => hasMore(this);
	}

	public interface IResult<Token, TValue>
	{
		T Match<T>(Func<FailResult<Token, TValue>, T> fail,
			Func<SuccessResult<Token, TValue>, T> success);
	}

	public class FailResult<Token, TValue> : IResult<Token, TValue>
	{
		public string Message { get; }
		public FailResult(string message) { Message = message; }
		public T Match<T>(Func<FailResult<Token, TValue>, T> fail,
			Func<SuccessResult<Token, TValue>, T> success) => fail(this);
	}

	public class SuccessResult<Token, TValue> : IResult<Token, TValue>
	{
		public TValue Value { get; }
		public ISource<Token> Next { get; }

		public SuccessResult(TValue value, ISource<Token> next) { Value = value; Next = next; }

		public T Match<T>(Func<FailResult<Token, TValue>, T> fail,
			Func<SuccessResult<Token, TValue>, T> success) => success(this);
	}


	public interface IRule<Token, TValue>
	{
		IResult<Token, TValue> TryParse(ISource<Token> source);
	}

	public static class StringSource
	{
		public static ISource<char> Create(string value, int index = 0)
		{
			if (index >= value.Length)
				return EmtySource<char>.Instance;

			return new SourceWithMoreContent<char>(value[index], () => Create(value, index + 1));
		}

		/*
		public static ISource<char> Create(string value, int index = 0)
			=> index >= value.Length
				? (ISource<char>)EmtySource<char>.Instance
				: new SourceWithMoreContent<char>(value[index], () => Create(value, index + 1));
		*/
	}

	public abstract class CharMatches : IRule<char, char>
	{
		protected abstract bool IsCharMatch(char c);

		public IResult<char, char> TryParse(ISource<char> source)
		{
			var result = source.Match(
				empty => (IResult<char, char>)new FailResult<char, char>("Unexpected EOF"),
				hasMore =>
				{
					if (!IsCharMatch(hasMore.Current))
						return new FailResult<char, char>($"Unexpected char: {hasMore.Current}");

					return new SuccessResult<char, char>(hasMore.Current, hasMore.Next());
				});

			return result;
		}

		/*
		public IResult<char, char> TryParse(ISource<char> source)
			=> source.Match(
				empty => new FailResult<char, char>("Unexpected EOF"),
				hasMore => IsCharMatch(hasMore.Current)
					? new SuccessResult<char, char>(hasMore.Current, hasMore.Next())
					: (IResult<char, char>)new FailResult<char, char>($"Unexpected char: {hasMore.Current}")
				);
		*/
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

		public IResult<Token, TResult[]> TryParse(ISource<Token> source)
		{
			List<TResult> buffer = new List<TResult>();
			bool shouldContinue = true;
			while (shouldContinue && !(source is EmtySource<Token>))
			{
				shouldContinue = rule.TryParse(source)
					.Match(
						fail => false,
						success =>
						{
							buffer.Add(success.Value);
							source = success.Next;
							return true;
						}
					);
			}

			if (requireAtLeastOne && buffer.Count == 0)
				return new FailResult<Token, TResult[]>("Expected at least one match");

			return new SuccessResult<Token, TResult[]>(buffer.ToArray(), source);
		}
	}

	public class FirstMatch<Token, TResult> : IRule<Token, TResult>
	{
		readonly IRule<Token, TResult>[] rules;
		public FirstMatch(IRule<Token, TResult>[] rules) { this.rules = rules; }

		public IResult<Token, TResult> TryParse(ISource<Token> source)
		{
			IResult<Token, TResult> result = new FailResult<Token, TResult>("No rule matched");
			int x = 0;
			while (x < rules.Length && result is FailResult<Token, TResult>)
			{
				result = rules[x].TryParse(source);
				x += 1;
			}

			return result;
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

		public IResult<Token, TOut> TryParse(ISource<Token> source)
		{
			var leftResult = leftRule.TryParse(source);
			var finalResult = leftResult.Match(
				leftFail => new FailResult<Token, TOut>(leftFail.Message),
				leftSuccess =>
				{
					var rightResult = rightRule.TryParse(leftSuccess.Next);
					var rightFinalResult = rightResult.Match(
						rightFail => (IResult<Token, TOut>)new FailResult<Token, TOut>(rightFail.Message),
						rightSuccess =>
						{
							var finalValue = Combine(leftSuccess.Value, rightSuccess.Value);
							return new SuccessResult<Token, TOut>(finalValue, rightSuccess.Next);
						});
					return rightFinalResult;
				});

			return finalResult;
		}

		/*
		public IResult<Token, TOut> TryParse(ISource<Token> source)
			=> leftRule.TryParse(source).Match(
				leftFail => new FailResult<Token, TOut>(leftFail.Message),
				leftSuccess =>
					rightRule.TryParse(leftSuccess.Next).Match(
						rightFail => (IResult<Token, TOut>)new FailResult<Token, TOut>(rightFail.Message),
						rightSuccess => new SuccessResult<Token, TOut>(Combine(leftSuccess.Value, rightSuccess.Value), rightSuccess.Next)
					)
			);
		*/
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

		public IResult<Token, Token> TryParse(ISource<Token> source)
		{
			var result = source.Match(
				empty => new FailResult<Token, Token>("Unexpected EOF"),
				current => rule.TryParse(current).Match(
					fail => new SuccessResult<Token, Token>(current.Current, current.Next()),
					success => (IResult<Token, Token>)new FailResult<Token, Token>("Unexpected match")
				)
			);

			return result;
		}

		/*
		public IResult<Token, Token> TryParse(ISource<Token> source)
			=> source.Match(
				empty => new FailResult<Token, Token>("Unexpected EOF"),
				current => rule.TryParse(current).Match(
					fail => new SuccessResult<Token, Token>(current.Current, current.Next()),
					success => (IResult<Token, Token>)new FailResult<Token, Token>("Unexpected match")
				)
			);
		*/
	}

	public abstract class MapTo<Token, TIn, TOut> : IRule<Token, TOut>
	{
		readonly IRule<Token, TIn> rule;
		protected MapTo(IRule<Token, TIn> rule) { this.rule = rule; }

		protected abstract TOut Convert(TIn value);

		public IResult<Token, TOut> TryParse(ISource<Token> source)
		{
			var result = rule.TryParse(source);
			return result.Match(
				fail => (IResult<Token, TOut>)new FailResult<Token, TOut>(fail.Message),
				success =>
				{
					var value = Convert(success.Value);
					return new SuccessResult<Token, TOut>(value, success.Next);
				}
			);
		}

		/*
		public IResult<Token, TOut> TryParse(ISource<Token> source)
			=> rule.TryParse(source).Match(
				fail => (IResult<Token, TOut>)new FailResult<Token, TOut>(fail.Message),
				success => new SuccessResult<Token, TOut>(Convert(success.Value), success.Next)
			);
		*/
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

			var source = StringSource.Create(raw);

			var asQuote = fullQuote.TryParse(source);
			return asQuote.Match(
				quoteFail => digitsAsInt.TryParse(source).Match(
						digitFail => null,
						digitSuccess => (object)digitSuccess.Value
					),
				quoteSuccess => (object)quoteSuccess.Value
			);
		}
	}
}