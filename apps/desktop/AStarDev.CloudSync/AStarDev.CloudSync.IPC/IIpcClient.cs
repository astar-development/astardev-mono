using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>
/// UI side of the IPC contract. Business code depends on this, never on a transport.
/// Failures are <see cref="IpcError"/> values; cancellation surfaces as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IIpcClient : IAsyncDisposable
{
    Task<Result<Unit, IpcError>> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Completes when the connection ends: the server went away or the client was disposed. One client is one connection.</summary>
    Task Disconnected { get; }

    Task<Result<CommandResult, IpcError>> SendAsync(ICommand command, CancellationToken cancellationToken);

    // shortcut: cancelling a request abandons the wait, the server still runs the handler; add a cancel envelope when handlers get expensive.
    Task<Result<TResult, IpcError>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);

    /// <summary>Register before <see cref="SubscribeAsync"/>; events with no handler are dropped. A failing handler is reported, not fatal.</summary>
    // shortcut: no unsubscribe, the client lives as long as its connection; return IDisposable when a view needs it.
    void OnEvent<TEvent>(Action<TEvent> handler) where TEvent : IEvent;

    /// <summary>Opens the long-lived event subscription; completes once the server has registered it.</summary>
    Task<Result<Unit, IpcError>> SubscribeAsync(CancellationToken cancellationToken);
}
