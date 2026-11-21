namespace AStarDev.FunctionalParadigm;

/// <summary>
/// Factories for <see cref="Exceptional{T}" /> that infer the case type from the argument.
/// </summary>
/// <example>
/// <code>
/// Exceptional&lt;int&gt; ok = Exceptional.Success(42);
/// Exceptional&lt;int&gt; failed = Exceptional.Failure&lt;int&gt;(new InvalidOperationException("no value"));
/// </code>
/// </example>
public static class Exceptional
{
    /// <summary>Wraps a value as a <see cref="Success{T}" />.</summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>An <see cref="Exceptional{T}" /> in the <see cref="Success{T}" /> state.</returns>
    public static Exceptional<T> Success<T>(T value) => new Success<T>(value);

    /// <summary>Wraps an exception as a <see cref="Failure{T}" />. The exception is stored, not thrown.</summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="exception">The captured exception.</param>
    /// <returns>An <see cref="Exceptional{T}" /> in the <see cref="Failure{T}" /> state.</returns>
    public static Exceptional<T> Failure<T>(Exception exception) => new Failure<T>(exception);
}
