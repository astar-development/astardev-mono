using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>
/// UI side of the IPC contract. Business code depends on this, never on a transport.
/// Failures are <see cref="IpcError"/> values; cancellation surfaces as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IIpcClient : IAsyncDisposable
{
    /// <summary>Opens the connection. Call before any other member. A failed or cancelled attempt leaves nothing open and can be retried on the same client; once connected, a second call fails.</summary>
    /// <returns><see cref="ConnectionFailed"/> when the server is not reachable, or the client is already connected or disposed.</returns>
    Task<Result<Unit, IpcError>> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Completes when the connection ends: the server went away or the client was disposed. One client is one connection.</summary>
    Task Disconnected { get; }

    /// <summary>Sends a command and waits for the daemon's answer.</summary>
    /// <returns>The <see cref="CommandResult"/>, or <see cref="NotConnected"/> before <see cref="ConnectAsync"/>, or <see cref="HandlerFailed"/> when the daemon could not handle it.</returns>
    Task<Result<CommandResult, IpcError>> SendAsync(ICommand command, CancellationToken cancellationToken);

    // shortcut: cancelling a request abandons the wait, the server still runs the handler; add a cancel envelope when handlers get expensive.
    /// <summary>Asks the daemon for state and waits for the answer. The result type comes from the query, so no cast is needed.</summary>
    /// <example>
    /// <code>
    /// var result = await client.QueryAsync(new GetDaemonStatus(), cancellationToken);
    /// var state = result switch
    /// {
    ///     Ok&lt;DaemonStatusView&gt; ok => ok.Value.State.ToString(),
    ///     Fail&lt;IpcError&gt; fail => fail.Error.ToString()
    /// };
    /// </code>
    /// </example>
    Task<Result<TResult, IpcError>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);

    /// <summary>Register before <see cref="SubscribeAsync"/>; events with no handler are dropped. A failing handler is reported, not fatal.</summary>
    // shortcut: no unsubscribe, the client lives as long as its connection; return IDisposable when a view needs it.
    void OnEvent<TEvent>(Action<TEvent> handler) where TEvent : IEvent;

    /// <summary>Opens the long-lived event subscription; completes once the server has registered it.</summary>
    Task<Result<Unit, IpcError>> SubscribeAsync(CancellationToken cancellationToken);
}
