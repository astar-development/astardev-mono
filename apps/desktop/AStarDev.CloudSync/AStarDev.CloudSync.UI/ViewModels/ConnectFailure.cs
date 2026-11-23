using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Why connecting failed, worded for the user.</summary>
/// <param name="Message">Text to show as it is.</param>
/// <param name="TraceId">The daemon trace that handled the failed request, when the daemon sent one.</param>
/// <param name="Code">The code of the error, when the failure has one; <c>default</c> when it has none.</param>
internal sealed record ConnectFailure(string Message, Option<string> TraceId, ErrorCode Code = default)
{
    /// <summary>A failure the daemon sent no trace for.</summary>
    public ConnectFailure(string message) : this(message, Option.None<string>())
    {
    }

    /// <summary>Words a command the daemon declined, using <paramref name="fallback"/> when it gave no message, and keeps the trace it handled the request under.</summary>
    public static ConnectFailure Rejected(CommandResult result, string fallback) => new(result.Message ?? fallback, Option.FromNullable(result.TraceId));

    /// <summary>
    /// Words an IPC failure. A <see cref="HandlerFailed"/> message comes from the daemon already worded for the user and is passed through, with the code the daemon reported or else its own; a lost or
    /// refused connection means the daemon is not running; anything else is a reply the UI cannot use.
    /// </summary>
    public static ConnectFailure From(IpcError error) => error switch
    {
        HandlerFailed e => new ConnectFailure(e.Message, Option.None<string>(), Reported(e)),
        ConnectionFailed e => new ConnectFailure(ApplicationConstants.DaemonUnreachableText, Option.None<string>(), e.Code),
        NotConnected e => new ConnectFailure(ApplicationConstants.DaemonUnreachableText, Option.None<string>(), e.Code),
        ConnectionLost e => new ConnectFailure(ApplicationConstants.DaemonUnreachableText, Option.None<string>(), e.Code),
        FrameTooLarge e => new ConnectFailure(ApplicationConstants.UnexpectedReplyText, Option.None<string>(), e.Code),
        MalformedFrame e => new ConnectFailure(ApplicationConstants.UnexpectedReplyText, Option.None<string>(), e.Code),
        ListenFailed e => new ConnectFailure(ApplicationConstants.UnexpectedReplyText, Option.None<string>(), e.Code)
    };

    private static ErrorCode Reported(HandlerFailed error) => string.IsNullOrEmpty(error.Reported.Value) ? error.Code : error.Reported;
}
