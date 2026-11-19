namespace AStarDev.CloudSync.IPC;

public sealed class UnixSocketIpcClient(string socketPath) : IIpcClient
{
    public Task Disconnected => throw new NotImplementedException();

    public Task ConnectAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CommandResult> SendAsync(ICommand command, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public void OnEvent<TEvent>(Action<TEvent> handler) where TEvent : IEvent => throw new NotImplementedException();

    public Task SubscribeAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
