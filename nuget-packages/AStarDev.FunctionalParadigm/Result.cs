namespace AStarDev.FunctionalParadigm;

/// <summary>
/// The outcome of an operation that can fail in an expected way: either <see cref="Ok{TResult}" /> carrying the value
/// or <see cref="Fail{TError}" /> carrying a typed error. Return it instead of throwing for failures the caller should handle.
/// </summary>
/// <typeparam name="TResult">The value of a success.</typeparam>
/// <typeparam name="TError">The error of a failure. Prefer a closed set of types, such as another union, so callers can match exhaustively.</typeparam>
/// <remarks>
/// Cancellation is not a failure: let <see cref="OperationCanceledException" /> propagate.
/// Use <see cref="Try" /> to turn code that throws into an <see cref="Exceptional{T}" />, then
/// <c>ToResult</c> to cross into a <see cref="Result{TResult,TError}" />.
/// </remarks>
/// <example>
/// <code>
/// Result&lt;int, string&gt; Parse(string text) =>
///     int.TryParse(text, out var number) ? new Ok&lt;int&gt;(number) : new Fail&lt;string&gt;($"not a number: {text}");
///
/// var message = Parse("42") switch
/// {
///     Ok&lt;int&gt; ok => $"got {ok.Value}",
///     Fail&lt;string&gt; fail => fail.Error
/// };
/// </code>
/// </example>
public union Result<TResult, TError>(Ok<TResult>, Fail<TError>);

/// <summary>The success case of <see cref="Result{TResult,TError}" />.</summary>
/// <param name="Value">What the operation produced. Use <see cref="Unit" /> when it produces nothing.</param>
public sealed record Ok<TResult>(TResult Value);

/// <summary>The failure case of <see cref="Result{TResult,TError}" />.</summary>
/// <param name="Error">Why the operation failed, as a value the caller can inspect rather than an exception.</param>
public sealed record Fail<TError>(TError Error);
