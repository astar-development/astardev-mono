using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>Serves the IPC contract over a Unix domain socket, one length-prefixed JSON frame per envelope.</summary>
/// <param name="socketPath">Where the socket file is created.</param>
/// <param name="onError">Told about failures nobody else can receive: a broken peer, a dying accept loop.</param>
public sealed class UnixSocketIpcServer(string socketPath, Action<IpcError>? onError = null) : IIpcServer
{
    public void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<Result<CommandResult, IpcError>>> handler)
        where TCommand : ICommand => throw new NotImplementedException();

    public void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<Result<TResult, IpcError>>> handler)
        where TQuery : IQuery<TResult> => throw new NotImplementedException();

    public Task<Result<Unit, IpcError>> StartAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<Result<Unit, IpcError>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent => throw new NotImplementedException();

    public Task<Result<Unit, IpcError>> StopAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
