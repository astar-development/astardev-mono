namespace AStarDev.FunctionalParadigm;

public static class Try
{
    public static Result<T, Exception> Run<T>(Func<T> operation)
    {
        try
        {
            return new Ok<T>(operation());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new Fail<Exception>(exception);
        }
    }

    public static async Task<Result<T, Exception>> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return new Ok<T>(await operation(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new Fail<Exception>(exception);
        }
    }
}
