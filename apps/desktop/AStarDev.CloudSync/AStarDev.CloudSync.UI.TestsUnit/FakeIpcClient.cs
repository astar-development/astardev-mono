using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeIpcClient(List<string> calls, bool available) : IIpcClient
{
    private readonly TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Result<CommandResult, IpcError> CommandReply { get; init; } = new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));

    public Result<RemoteRootSummaryView, IpcError> SummaryReply { get; init; } = new Ok<RemoteRootSummaryView>(new RemoteRootSummaryView(0));

    public List<ICommand> Sent { get; } = [];

    public TaskCompletionSource? HoldCommand { get; init; }

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

    public async Task<Result<CommandResult, IpcError>> SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add("send");
        Sent.Add(command);
        if (HoldCommand is not null) await HoldCommand.Task.WaitAsync(cancellationToken);

        return CommandReply;
    }

    public Task<Result<TResult, IpcError>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add("query");

        return Task.FromResult(query switch
        {
            GetRemoteRootSummary => (Result<TResult, IpcError>)(object)SummaryReply,
            _ => new Ok<TResult>((TResult)(object)Status)
        });
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
