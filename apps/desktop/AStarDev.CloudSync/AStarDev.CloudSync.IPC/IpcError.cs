namespace AStarDev.CloudSync.IPC;

/// <summary>A connection could not be opened.</summary>
public sealed record ConnectionFailed(string Reason);

/// <summary>The client was asked to talk before it ever connected.</summary>
public sealed record NotConnected;

/// <summary>An open connection ended or broke: the peer went away, the socket reset.</summary>
public sealed record ConnectionLost(string Reason);

/// <summary>A frame is bigger than <see cref="MaxSize"/>, so it was not sent or not read.</summary>
public sealed record FrameTooLarge(long Size, int MaxSize);

/// <summary>Bytes arrived or were produced that are not a valid frame or envelope.</summary>
public sealed record MalformedFrame(string Reason);

/// <summary>The server answered the request with a failure: its handler failed, threw, or was not mapped.</summary>
public sealed record HandlerFailed(string Message);

/// <summary>The server could not start or keep listening.</summary>
public sealed record ListenFailed(string Reason);

/// <summary>Everything that can go wrong in IPC, as values. Match it exhaustively.</summary>
public union IpcError(ConnectionFailed, NotConnected, ConnectionLost, FrameTooLarge, MalformedFrame, HandlerFailed, ListenFailed);

public static class IpcErrorExtensions
{
    public static string Describe(this IpcError error) => error switch
    {
        ConnectionFailed e => $"Could not connect: {e.Reason}",
        NotConnected => "The IPC client is not connected.",
        ConnectionLost e => $"The IPC connection was lost: {e.Reason}",
        FrameTooLarge e => $"A frame of {e.Size} bytes exceeds the maximum of {e.MaxSize}.",
        MalformedFrame e => $"Malformed frame: {e.Reason}",
        HandlerFailed e => e.Message,
        ListenFailed e => $"The IPC server cannot listen: {e.Reason}"
    };
}
