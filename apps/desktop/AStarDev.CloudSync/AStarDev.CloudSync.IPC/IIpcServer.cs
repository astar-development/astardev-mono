using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Daemon side of the IPC contract. Business code depends on this, never on a transport.
/// Failures are <see cref="IpcError"/> values; cancellation surfaces as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IIpcServer : IAsyncDisposable
{
    /// <summary>Handlers must be mapped before <see cref="StartAsync"/>. A handler that throws is reported as <see cref="HandlerFailed"/>.</summary>
    void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<Result<CommandResult, IpcError>>> handler)
        where TCommand : ICommand;

    void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<Result<TResult, IpcError>>> handler)
        where TQuery : IQuery<TResult>;

    /// <summary>
    /// Starts listening. <paramref name="cancellationToken"/> also bounds the server's lifetime:
    /// cancelling it stops the accept loop and closes every connection, like <see cref="StopAsync"/>.
    /// </summary>
    Task<Result<Unit, IpcError>> StartAsync(CancellationToken cancellationToken);

    /// <summary>Pushes the event to every connection that has subscribed. A subscriber that is gone is skipped.</summary>
    Task<Result<Unit, IpcError>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent;

    /// <summary>
    /// Stops listening and closes every connection. Cancelling the token only stops the wait for the
    /// shutdown to finish, the shutdown itself is already under way. Returns the failure that ended the
    /// accept loop early, if there was one.
    /// </summary>
    Task<Result<Unit, IpcError>> StopAsync(CancellationToken cancellationToken);
}
