namespace AStarDev.CloudSync.Errors;

/// <summary>Reads the code of an error held in a union, so a log line can carry it without knowing the case.</summary>
public static class ErrorInfo
{
    /// <summary>Reads the code of <paramref name="error"/>, usually the <c>Value</c> of an error union.</summary>
    /// <param name="error">The error case. A value that is not an <see cref="IErrorInfo"/> has no code.</param>
    /// <returns>The code of the error, or <c>default</c> (no code) when <paramref name="error"/> is not an <see cref="IErrorInfo"/>.</returns>
    public static ErrorCode CodeOf(object? error) => error is IErrorInfo info ? info.Code : default;
}
