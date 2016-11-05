using System;
using System.Collections.Generic;

namespace ParsingDemo.Iteration30
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

	public delegate IResult<Token, TValue> Rule<Token, TValue>(ISource<Token> source);

	public static class StringSource
	{
		public static ISource<char> Create(string value, int index = 0)
			=> index >= value.Length
				? (ISource<char>)EmtySource<char>.Instance
				: new SourceWithMoreContent<char>(value[index], () => Create(value, index + 1));
	}

	public static class Rules
	{

		public static Rule<char, char> CharMatches(Func<char, bool> isMatch)
			=> (source) => source.Match(
				empty => new FailResult<char, char>("Unexpected EOF"),
				hasMore => isMatch(hasMore.Current)
					? new SuccessResult<char, char>(hasMore.Current, hasMore.Next())
					: (IResult<char, char>)new FailResult<char, char>($"Unexpected char: {hasMore.Current}")
				);

		public static Rule<char, char> CharIsDigit() => CharMatches(char.IsDigit);

		public static Rule<char, char> CharIs(char c) => CharMatches(x => x == c);

		public static Rule<Token, TResult[]> Many<Token, TResult>(this Rule<Token, TResult> rule, bool requireAtLeastOne = false)
			=> (source) =>
			{
				List<TResult> buffer = new List<TResult>();
				bool shouldContinue = true;
				while (shouldContinue && !(source is EmtySource<Token>))
				{
					shouldContinue = rule(source)
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

			};

		public static Rule<Token, TResult> FirstMatch<Token, TResult>(params Rule<Token, TResult>[] rules)
			=> (source) =>
			{
				IResult<Token, TResult> result = new FailResult<Token, TResult>("No rule matched");
				int x = 0;
				while (x < rules.Length && result is FailResult<Token, TResult>)
				{
					result = rules[x](source);
					x += 1;
				}

				return result;
			};

		public static Rule<Token, TOut> MatchThen<Token, TLeft, TRight, TOut>(this Rule<Token, TLeft> leftRule, Rule<Token, TRight> rightRule, Func<TLeft, TRight, TOut> convert)
			=> (source) => leftRule(source).Match(
				leftFail => new FailResult<Token, TOut>(leftFail.Message),
				leftSuccess =>
					rightRule(leftSuccess.Next).Match(
						rightFail => (IResult<Token, TOut>)new FailResult<Token, TOut>(rightFail.Message),
						rightSuccess => new SuccessResult<Token, TOut>(convert(leftSuccess.Value, rightSuccess.Value), rightSuccess.Next)
					)
			);

		public static Rule<Token, TRight> MatchThenKeep<Token, TLeft, TRight>(this Rule<Token, TLeft> leftRule, Rule<Token, TRight> rightRule)
			=> MatchThen(leftRule, rightRule, (left, right) => right);

		public static Rule<Token, TLeft> MatchThenIgnore<Token, TLeft, TRight>(this Rule<Token, TLeft> leftRule, Rule<Token, TRight> rightRule)
			=> MatchThen(leftRule, rightRule, (left, right) => left);

		public static Rule<Token, Token> Not<Token, TResult>(this Rule<Token, TResult> rule)
			=> (source) => source.Match(
				empty => new FailResult<Token, Token>("Unexpected EOF"),
				current => rule(current).Match(
					fail => new SuccessResult<Token, Token>(current.Current, current.Next()),
					success => (IResult<Token, Token>)new FailResult<Token, Token>("Unexpected match")
				)
			);

		public static Rule<Token, TOut> MapTo<Token, TIn, TOut>(this Rule<Token, TIn> rule, Func<TIn, TOut> convert)
			=> (source) => rule(source).Match(
				fail => (IResult<Token, TOut>)new FailResult<Token, TOut>(fail.Message),
				success => new SuccessResult<Token, TOut>(convert(success.Value), success.Next)
			);

		public static Rule<char, string> JoinText(this Rule<char, char[]> rule)
			=> MapTo(rule, (x) => new string(x));

		public static Rule<char, int> MapToInteger(this Rule<char, string> rule)
			=> MapTo(rule, (x) => int.Parse(x));
	}

	public static class Test
	{
		public static object Parse(string raw)
		{
			var quote = Rules.CharIs('"');
			var slash = Rules.CharIs('\\');
			var escapedQuote = slash.MatchThenKeep(quote);
			var escapedSlash = slash.MatchThenKeep(slash);
			var notQuote = quote.Not();

			var fullQuote = quote
				.MatchThenKeep(
					Rules.FirstMatch(
						escapedQuote,
						escapedSlash,
						notQuote
					).Many().JoinText()
				)
				.MatchThenIgnore(quote);

			var digit = Rules.CharIsDigit()
				.Many(true)
				.JoinText()
				.MapToInteger();

			var finalResult = Rules.FirstMatch(
				fullQuote.MapTo(x => (object)x),
				digit.MapTo(x => (object)x)
			);

			var source = StringSource.Create(raw);

			return finalResult(source).Match(
				fail => null,
				success => success.Value
			);
		}
	}
}