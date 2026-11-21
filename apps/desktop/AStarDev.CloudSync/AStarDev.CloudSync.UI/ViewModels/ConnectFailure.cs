using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Why connecting failed, worded for the user.</summary>
/// <param name="Message">Text to show as it is.</param>
internal sealed record ConnectFailure(string Message)
{
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
