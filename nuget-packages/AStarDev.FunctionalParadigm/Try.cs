namespace AStarDev.FunctionalParadigm;

/// <summary>
/// Where exceptions are captured. Wrap the code that can throw, and everything after it is a value pipeline
/// (see <see cref="ExceptionalExtensions" />) instead of a try/catch.
/// </summary>
/// <remarks>
/// <see cref="OperationCanceledException" /> is never captured: cancellation is not a failure, so it propagates.
/// </remarks>
public static class Try
{
    /// <summary>Runs <paramref name="operation" /> and captures any exception it throws, except cancellation.</summary>
    /// <param name="operation">The code that may throw.</param>
    /// <returns>A <see cref="Success{T}" /> with the return value, or a <see cref="Failure{T}" /> with the exception.</returns>
    /// <example>
    /// <code>
    /// var parsed = Try.Run(() => JsonSerializer.Deserialize&lt;Settings&gt;(json));
    /// </code>
    /// </example>
    public static Exceptional<T> Run<T>(Func<T> operation)
    {
        try
        {
            return new Success<T>(operation());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new Failure<T>(exception);
        }
    }

    /// <summary>
    /// Asynchronous <see cref="Run{T}" />. Throws <see cref="OperationCanceledException" /> without running
    /// <paramref name="operation" /> when the token is already cancelled.
    /// </summary>
    /// <param name="operation">The code that may throw. It receives <paramref name="cancellationToken" />; honour it.</param>
    /// <param name="cancellationToken">Passed to the operation. Cancellation propagates; it is never captured.</param>
    /// <returns>A <see cref="Success{T}" /> with the result, or a <see cref="Failure{T}" /> with the exception.</returns>
    /// <example>
    /// <code>
    /// var response = await Try.RunAsync(token => client.GetStringAsync(url, token), cancellationToken);
    /// </code>
    /// </example>
    public static async Task<Exceptional<T>> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return new Success<T>(await operation(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new Failure<T>(exception);
        }
    }
}
