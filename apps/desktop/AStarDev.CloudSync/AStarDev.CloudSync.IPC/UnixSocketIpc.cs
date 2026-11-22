using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.IPC;

/// <summary>
/// The one place that creates the Unix domain socket transport. Callers hold the result as
/// <see cref="IIpcServer"/> or <see cref="IIpcClient"/>, so the transport can be replaced without touching them.
/// </summary>
public static class UnixSocketIpc
{
    /// <summary>
    /// Creates the daemon side. Nothing listens until <see cref="IIpcServer.StartAsync"/> is called.
    /// </summary>
    /// <param name="socketPath">Socket file the server creates. Must be the path the client is given.</param>
    /// <param name="onError">Told about failures that cannot be returned to any caller, such as a broken peer. Omit to ignore them.</param>
    /// <param name="logger">Receives structured logs of connections and failed requests, never of message contents. Omit to log nothing.</param>
    /// <example>
    /// <code>
    /// await using var server = UnixSocketIpc.CreateServer("/run/user/1000/cloudsync.sock");
    /// server.MapQuery((GetDaemonStatus _, CancellationToken _) => Task.FromResult&lt;Result&lt;DaemonStatusView, IpcError&gt;&gt;(new Ok&lt;DaemonStatusView&gt;(status)));
    /// var started = await server.StartAsync(cancellationToken);
    /// </code>
    /// </example>
    public static IIpcServer CreateServer(string socketPath, Action<IpcError>? onError = null, ILogger? logger = null)
        => new UnixSocketIpcServer(socketPath, onError, logger);

    /// <summary>
    /// Creates the UI side. One client is one connection: call <see cref="IIpcClient.ConnectAsync"/> once, and create a new client to reconnect.
    /// </summary>
    /// <param name="socketPath">Socket file the daemon's server created.</param>
    /// <param name="onError">Told about failures that cannot be returned to any caller, such as a failing event handler. Omit to ignore them.</param>
    /// <param name="logger">Receives structured logs of connections and failed requests, never of message contents. Omit to log nothing.</param>
    /// <example>
    /// <code>
    /// await using var client = UnixSocketIpc.CreateClient("/run/user/1000/cloudsync.sock");
    /// var connected = await client.ConnectAsync(cancellationToken);
    /// var status = await client.QueryAsync(new GetDaemonStatus(), cancellationToken);
    /// </code>
    /// </example>
    public static IIpcClient CreateClient(string socketPath, Action<IpcError>? onError = null, ILogger? logger = null)
        => new UnixSocketIpcClient(socketPath, onError, logger);
}
