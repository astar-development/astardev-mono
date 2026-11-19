namespace AStarDev.CloudSync.IPC;

public sealed class UnixSocketIpcServer(string socketPath) : IIpcServer
{
    public void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<CommandResult>> handler)
        where TCommand : ICommand => throw new NotImplementedException();

    public void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<TResult>> handler)
        where TQuery : IQuery<TResult> => throw new NotImplementedException();

    public Task StartAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent => throw new NotImplementedException();

    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
