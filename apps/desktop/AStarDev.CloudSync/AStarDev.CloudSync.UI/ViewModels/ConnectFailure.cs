using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Why connecting failed, worded for the user.</summary>
/// <param name="Message">Text to show as it is.</param>
/// <param name="TraceId">The daemon trace that handled the failed request, when the daemon sent one.</param>
internal sealed record ConnectFailure(string Message, Option<string> TraceId)
{
    /// <summary>A failure the daemon sent no trace for.</summary>
    public ConnectFailure(string message) : this(message, Option.None<string>())
    {
    }

    /// <summary>Words a command the daemon declined, using <paramref name="fallback"/> when it gave no message, and keeps the trace it handled the request under.</summary>
    public static ConnectFailure Rejected(CommandResult result, string fallback) => new(result.Message ?? fallback, Option.FromNullable(result.TraceId));

    /// <summary>
    /// Words an IPC failure. A <see cref="HandlerFailed"/> message comes from the daemon already worded for the user and is passed through; a lost or
    /// refused connection means the daemon is not running; anything else is a reply the UI cannot use.
    /// </summary>
    public static ConnectFailure From(IpcError error) => error switch
    {
        HandlerFailed e => new ConnectFailure(e.Message),
        ConnectionFailed or NotConnected or ConnectionLost => new ConnectFailure(ApplicationConstants.DaemonUnreachableText),
        FrameTooLarge or MalformedFrame or ListenFailed => new ConnectFailure(ApplicationConstants.UnexpectedReplyText)
    };
}
