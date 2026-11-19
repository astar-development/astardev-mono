namespace AStarDev.CloudSync.IPC;

/// <summary>Daemon side of the IPC contract. Business code depends on this, never on a transport.</summary>
public interface IIpcServer : IAsyncDisposable
{
    /// <summary>Handlers must be mapped before <see cref="StartAsync"/>.</summary>
    void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<CommandResult>> handler)
        where TCommand : ICommand;

    void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<TResult>> handler)
        where TQuery : IQuery<TResult>;

    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Pushes the event to every connection that has subscribed.</summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent;
}
