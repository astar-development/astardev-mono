using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>Talks to a <see cref="UnixSocketIpcServer"/>. One instance is one connection; create a new one to reconnect.</summary>
/// <param name="socketPath">The server's socket file.</param>
/// <param name="onError">Told about failures nobody else can receive: a failing event handler, a broken frame.</param>
public sealed class UnixSocketIpcClient(string socketPath, Action<IpcError>? onError = null) : IIpcClient
{
    public Task Disconnected => throw new NotImplementedException();

    public Task<Result<Unit, IpcError>> ConnectAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<Result<CommandResult, IpcError>> SendAsync(ICommand command, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result<TResult, IpcError>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public void OnEvent<TEvent>(Action<TEvent> handler) where TEvent : IEvent => throw new NotImplementedException();

    public Task<Result<Unit, IpcError>> SubscribeAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
