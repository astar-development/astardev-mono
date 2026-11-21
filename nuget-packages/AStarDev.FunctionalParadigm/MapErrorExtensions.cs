namespace AStarDev.FunctionalParadigm;

/// <summary>Operators on <see cref="Result{TResult,TError}" />.</summary>
public static class MapErrorExtensions
{
    /// <summary>
    /// Translates the error of a failure and passes a success through untouched. Use it where one module's errors
    /// become another's, so internal error types do not leak across the boundary.
    /// </summary>
    /// <param name="result">The result whose error is translated.</param>
    /// <param name="map">Runs only for a <see cref="Fail{TError}" />. An exception it throws propagates.</param>
    /// <returns>The same <see cref="Ok{TResult}" />, or a <see cref="Fail{TError}" /> holding the translated error.</returns>
    /// <example>
    /// <code>
    /// Result&lt;Stream, ProviderError&gt; opened = Open(path).MapError(error => new Transient(error.Message));
    /// </code>
    /// </example>
    public static Result<TResult, TNewError> MapError<TResult, TError, TNewError>(this Result<TResult, TError> result, Func<TError, TNewError> map) =>
        result switch
        {
            Ok<TResult> ok => ok,
            Fail<TError> fail => new Fail<TNewError>(map(fail.Error))
        };
}
