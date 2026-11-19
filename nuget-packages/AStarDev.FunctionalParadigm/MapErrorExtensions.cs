namespace AStarDev.FunctionalParadigm;

public static class MapErrorExtensions
{
    public static Result<TResult, TNewError> MapError<TResult, TError, TNewError>(
        this Result<TResult, TError> result,
        Func<TError, TNewError> map)
        => throw new NotImplementedException();
}
