namespace AStarDev.CloudSync.IPC;

/// <summary>Maps the exception captured by <c>Try.Run</c> / <c>Try.RunAsync</c> onto an <see cref="IpcError"/>.</summary>
internal static class IpcErrors
{
    /// <summary>For a connect attempt that threw, such as a missing socket file or a refused connection.</summary>
    public static IpcError AsConnectionFailed(Exception exception) => new ConnectionFailed(exception.Message);

    /// <summary>For a read or write on an open connection that threw.</summary>
    public static IpcError AsConnectionLost(Exception exception) => new ConnectionLost(exception.Message);

    /// <summary>For JSON that could not be serialised or deserialised.</summary>
    public static IpcError AsMalformed(Exception exception) => new MalformedFrame(exception.Message);

    /// <summary>For a request handler on the server, or an event handler on the client, that threw.</summary>
    public static IpcError AsHandlerFailed(Exception exception) => new HandlerFailed(exception.Message);

    /// <summary>For binding, listening on, accepting from or removing the server socket, when that threw.</summary>
    public static IpcError AsListenFailed(Exception exception) => new ListenFailed(exception.Message);
}
