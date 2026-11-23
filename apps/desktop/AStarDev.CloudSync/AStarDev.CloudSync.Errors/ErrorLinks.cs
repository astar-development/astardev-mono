namespace AStarDev.CloudSync.Errors;

/// <summary>Builds the help link of an error code and stamps it on exceptions.</summary>
public static class ErrorLinks
{
    /// <summary>The root every help link sits under. Ends with <c>/</c>.</summary>
    public const string Root = "https://astar-development.co.uk/errors/";

    /// <summary>Builds the help link for <paramref name="code"/>: <see cref="Root"/> followed by the code.</summary>
    /// <param name="code">The code to link to.</param>
    /// <returns>The absolute link. The page behind it may not exist yet.</returns>
    public static string For(ErrorCode code) => Root + code.Value;

    /// <summary>Sets <see cref="Exception.HelpLink"/> to the link of <paramref name="code"/> and returns the same exception, ready to throw.</summary>
    /// <typeparam name="TException">The exception type, kept so the result can be thrown or returned unchanged.</typeparam>
    /// <param name="exception">The exception to stamp. Its previous <see cref="Exception.HelpLink"/> is replaced.</param>
    /// <param name="code">The code whose link is set.</param>
    /// <returns><paramref name="exception"/> itself.</returns>
    public static TException WithHelpLink<TException>(this TException exception, ErrorCode code)
        where TException : Exception
    {
        exception.HelpLink = For(code);

        return exception;
    }
}
