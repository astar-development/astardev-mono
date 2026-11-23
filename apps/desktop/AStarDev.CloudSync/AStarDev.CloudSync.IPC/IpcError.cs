using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.IPC;

/// <summary>A connection could not be opened.</summary>
public sealed record ConnectionFailed(string Reason) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => IpcErrorCodes.ConnectionFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The client was asked to talk before it ever connected.</summary>
public sealed record NotConnected : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => IpcErrorCodes.NotConnected;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>An open connection ended or broke: the peer went away, the socket reset.</summary>
public sealed record ConnectionLost(string Reason) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => IpcErrorCodes.ConnectionLost;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>A frame is bigger than <see cref="MaxSize"/>, so it was not sent or not read.</summary>
public sealed record FrameTooLarge(long Size, int MaxSize) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => IpcErrorCodes.FrameTooLarge;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>Bytes arrived or were produced that are not a valid frame or envelope.</summary>
public sealed record MalformedFrame(string Reason) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => IpcErrorCodes.MalformedFrame;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The server answered the request with a failure: its handler failed, threw, or was not mapped.</summary>
/// <param name="Message">Wording fit for display.</param>
/// <param name="Reported">The code the server sent with the failure. Empty (<c>default</c>) for a failure raised locally or sent without a readable code.</param>
public sealed record HandlerFailed(string Message, ErrorCode Reported = default) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => IpcErrorCodes.HandlerFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The server could not start or keep listening.</summary>
public sealed record ListenFailed(string Reason) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => IpcErrorCodes.ListenFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>Everything that can go wrong in IPC, as values. Match it exhaustively.</summary>
public union IpcError(ConnectionFailed, NotConnected, ConnectionLost, FrameTooLarge, MalformedFrame, HandlerFailed, ListenFailed);

/// <summary>Readable text for <see cref="IpcError"/> values.</summary>
internal static class IpcErrorExtensions
{
    /// <summary>The code and help link of whichever case <paramref name="error"/> holds.</summary>
    public static IErrorInfo Info(this IpcError error) => error switch
    {
        ConnectionFailed e => e,
        NotConnected e => e,
        ConnectionLost e => e,
        FrameTooLarge e => e,
        MalformedFrame e => e,
        HandlerFailed e => e,
        ListenFailed e => e
    };

    /// <summary>One sentence per error case. The server sends it as the <see cref="ErrorEnvelope"/> message, so it must make sense to the client.</summary>
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
