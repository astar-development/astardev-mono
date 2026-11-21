namespace AStarDev.FunctionalParadigm;

/// <summary>
/// The value of an operation that succeeds without producing anything, so it can still be a
/// <see cref="Result{TResult,TError}" /> (<c>void</c> cannot be a type argument).
/// </summary>
/// <example>
/// <code>
/// Task&lt;Result&lt;Unit, IpcError&gt;&gt; StartAsync(CancellationToken cancellationToken);
///
/// return new Ok&lt;Unit&gt;(default);
/// </code>
/// </example>
public readonly record struct Unit;
