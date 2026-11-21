using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeIpcClient(List<string> calls, bool available) : IIpcClient
{
    private readonly TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static DaemonStatusView Status { get; } = new(DaemonState.Running, "1.0.0", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

    public Task Disconnected => disconnected.Task;

    public void Drop() => disconnected.TrySetResult();

    public Task<Result<Unit, IpcError>> ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add("connect");

        return Task.FromResult<Result<Unit, IpcError>>(available
            ? new Ok<Unit>(default)
            : new Fail<IpcError>(new ConnectionFailed("no daemon")));
    }

    public Task<Result<CommandResult, IpcError>> SendAsync(ICommand command, CancellationToken cancellationToken)
        => Task.FromResult<Result<CommandResult, IpcError>>(new Fail<IpcError>(new NotConnected()));

    public Task<Result<TResult, IpcError>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add("query");

        return Task.FromResult<Result<TResult, IpcError>>(new Ok<TResult>((TResult)(object)Status));
    }

    public void OnEvent<TEvent>(Action<TEvent> handler)
        where TEvent : IEvent
    {
    }

    public Task<Result<Unit, IpcError>> SubscribeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add("subscribe");

        return Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
