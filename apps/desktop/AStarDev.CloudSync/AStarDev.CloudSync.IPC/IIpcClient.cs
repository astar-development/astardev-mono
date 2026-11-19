namespace AStarDev.CloudSync.IPC;

/// <summary>UI side of the IPC contract. Business code depends on this, never on a transport.</summary>
public interface IIpcClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Completes when the connection ends: the server went away or the client was disposed. One client is one connection.</summary>
    Task Disconnected { get; }

    /// <exception cref="IpcException">The server reported a failure.</exception>
    /// <exception cref="IOException">The connection is not open or was lost.</exception>
    Task<CommandResult> SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <exception cref="IpcException">The server reported a failure.</exception>
    /// <exception cref="IOException">The connection is not open or was lost.</exception>
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);

    /// <summary>Register before <see cref="SubscribeAsync"/>; events with no handler are dropped.</summary>
    // shortcut: no unsubscribe, the client lives as long as its connection; return IDisposable when a view needs it.
    void OnEvent<TEvent>(Action<TEvent> handler) where TEvent : IEvent;

    /// <summary>Opens the long-lived event subscription; completes once the server has registered it.</summary>
    Task SubscribeAsync(CancellationToken cancellationToken = default);
}
