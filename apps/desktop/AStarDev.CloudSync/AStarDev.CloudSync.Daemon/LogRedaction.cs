using System.Text.RegularExpressions;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Keeps personal data out of stored log messages, as a backstop for messages that should already carry ids and counts only.</summary>
internal static partial class LogRedaction
{
    private const string EmailPlaceholder = "<email>";
    private const string PathPlaceholder = "<path>";

    /// <summary>Replaces every e-mail address and every word containing a path separator with a placeholder.</summary>
    /// <param name="message">The rendered log message.</param>
    /// <returns>The message with nothing that looks like an e-mail address or a path left in it.</returns>
    public static string Scrub(string message) => PathPattern().Replace(EmailPattern().Replace(message, EmailPlaceholder), PathPlaceholder);

    [GeneratedRegex(@"[^\s@/\\]+@[^\s@/\\]+\.[^\s@/\\]+")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\S*[\\/]\S*")]
    private static partial Regex PathPattern();
}
