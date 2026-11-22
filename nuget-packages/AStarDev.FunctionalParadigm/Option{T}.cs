namespace AStarDev.FunctionalParadigm;

/// <summary>
/// A value that may be absent: either <see cref="Some{T}" /> carrying it or <see cref="None{T}" />. Use it instead of
/// returning or passing <c>null</c> inside the application; <c>null</c> is only allowed at a public edge.
/// </summary>
/// <remarks>
/// Use <see cref="Option" /> to construct instances, including from a nullable value, and
/// <see cref="OptionExtensions" /> or a <c>switch</c> over the two cases to consume them.
/// An absent value is not a failure; when the caller needs to know why, return a <see cref="Result{TResult,TError}" />.
/// </remarks>
/// <typeparam name="T">The type of the value when present.</typeparam>
/// <example>
/// <code>
/// Option&lt;int&gt; found = Option.Some(42);
/// var text = found.Match(value => $"got {value}", () => "nothing");
/// </code>
/// </example>
public union Option<T>(Some<T>, None<T>);

/// <summary>The present case of <see cref="Option{T}" />.</summary>
/// <param name="Value">The value. Never <c>null</c> when created through <see cref="Option.Some{T}" />.</param>
public sealed record Some<T>(T Value);

/// <summary>The absent case of <see cref="Option{T}" />.</summary>
public sealed record None<T>;
