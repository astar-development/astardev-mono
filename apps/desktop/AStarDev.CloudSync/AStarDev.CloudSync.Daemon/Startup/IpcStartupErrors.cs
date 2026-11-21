using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Maps IPC failures onto <see cref="StartupError"/> for the accept connections stage.</summary>
internal static class IpcStartupErrors
{
    /// <summary>
    /// One <see cref="StartupError"/> per <see cref="IpcError"/> case, named after the accept connections stage. The cause carries the case and its details;
    /// no case holds an exception, so <see cref="StartupError.Exception"/> stays <see langword="null"/>.
    /// </summary>
    public static StartupError From(IpcError error) => error switch
    {
        ConnectionFailed e => ForCause($"Could not connect: {e.Reason}"),
        NotConnected => ForCause("The IPC client is not connected."),
        ConnectionLost e => ForCause($"The IPC connection was lost: {e.Reason}"),
        FrameTooLarge e => ForCause($"A frame of {e.Size} bytes exceeds the maximum of {e.MaxSize}."),
        MalformedFrame e => ForCause($"Malformed frame: {e.Reason}"),
        HandlerFailed e => ForCause(e.Message),
        ListenFailed e => ForCause($"The IPC server cannot listen: {e.Reason}")
    };

    private static StartupError ForCause(string cause) => new(ApplicationConstants.AcceptConnectionsStageName, cause);
}
