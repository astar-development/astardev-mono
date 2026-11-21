namespace AStarDev.FunctionalParadigm;

/// <summary>
///     Represents the outcome of an operation that may either succeed with a
///     <typeparamref name="T" /> value or fail with a captured <see cref="Exception" />.
///     Use the <see cref="Exceptional" /> factory class to construct instances.
/// </summary>
/// <remarks>
///     Produce one with <see cref="Try.Run{T}" /> rather than a try/catch, and consume it with the operators in
///     <see cref="ExceptionalExtensions" /> or a <c>switch</c> over <see cref="Success{T}" /> and <see cref="Failure{T}" />.
/// </remarks>
/// <example>
/// <code>
/// Exceptional&lt;int&gt; fromValue = new Success&lt;int&gt;(42);
/// Exceptional&lt;int&gt; fromException = new Failure&lt;int&gt;(new InvalidOperationException("no value"));
/// </code>
/// </example>
/// <typeparam name="T">The type of the success value.</typeparam>
public union Exceptional<T>(Success<T>, Failure<T>);

/// <summary>The success case of <see cref="Exceptional{T}" />.</summary>
/// <param name="Value">What the operation produced.</param>
public sealed record Success<T>(T Value);

/// <summary>The failure case of <see cref="Exceptional{T}" />.</summary>
/// <param name="Exception">The captured exception. Rethrow it with <c>GetOrThrow</c> to keep its original stack trace.</param>
public sealed record Failure<T>(Exception Exception);
