namespace AStarDev.FunctionalParadigm;

/// <summary>Factories for <see cref="Option{T}" /> that infer the case type from the argument.</summary>
/// <example>
/// <code>
/// Option&lt;string&gt; present = Option.Some("a");
/// Option&lt;string&gt; absent = Option.None&lt;string&gt;();
/// Option&lt;string&gt; fromEdge = Option.FromNullable(externalValueThatMayBeNull);
/// </code>
/// </example>
public static class Option
{
    /// <summary>Wraps a value as a <see cref="Some{T}" />.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value. It cannot be <c>null</c>; use <see cref="FromNullable{T}(T?)" /> for a value that may be.</param>
    /// <returns>An <see cref="Option{T}" /> in the <see cref="Some{T}" /> state.</returns>
    public static Option<T> Some<T>(T value) where T : notnull => new Some<T>(value);

    /// <summary>Creates the absent case.</summary>
    /// <typeparam name="T">The type of the value when present.</typeparam>
    /// <returns>An <see cref="Option{T}" /> in the <see cref="None{T}" /> state.</returns>
    public static Option<T> None<T>() => new None<T>();

    /// <summary>Converts a reference that may be <c>null</c>, as received at a public edge, to an option.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The reference, or <c>null</c>.</param>
    /// <returns><see cref="None{T}" /> for <c>null</c>, otherwise <see cref="Some{T}" />.</returns>
    public static Option<T> FromNullable<T>(T? value) where T : class => value is null ? None<T>() : Some(value);

    /// <summary>Converts a nullable value type, as received at a public edge, to an option.</summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="value">The value, or <c>null</c>.</param>
    /// <returns><see cref="None{T}" /> when there is no value, otherwise <see cref="Some{T}" />.</returns>
    public static Option<T> FromNullable<T>(T? value) where T : struct => value is { } present ? Some(present) : None<T>();
}
