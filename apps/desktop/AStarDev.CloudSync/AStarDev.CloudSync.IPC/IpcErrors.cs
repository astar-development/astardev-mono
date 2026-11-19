namespace AStarDev.CloudSync.IPC;

/// <summary>Maps the exception captured by <c>Try.Run</c> / <c>Try.RunAsync</c> onto an <see cref="IpcError"/>.</summary>
internal static class IpcErrors
{
    public static IpcError AsConnectionFailed(Exception exception) => new ConnectionFailed(exception.Message);

    public static IpcError AsConnectionLost(Exception exception) => new ConnectionLost(exception.Message);

    public static IpcError AsMalformed(Exception exception) => new MalformedFrame(exception.Message);

    public static IpcError AsHandlerFailed(Exception exception) => new HandlerFailed(exception.Message);

    public static IpcError AsListenFailed(Exception exception) => new ListenFailed(exception.Message);
}
