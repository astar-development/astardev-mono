namespace AStarDev.FunctionalParadigm;

public static class MapErrorExtensions
{
    public static Result<TResult, TNewError> MapError<TResult, TError, TNewError>(
        this Result<TResult, TError> result,
        Func<TError, TNewError> map)
        => result switch
        {
            Ok<TResult> ok => ok,
            Fail<TError> fail => new Fail<TNewError>(map(fail.Error))
        };
}
