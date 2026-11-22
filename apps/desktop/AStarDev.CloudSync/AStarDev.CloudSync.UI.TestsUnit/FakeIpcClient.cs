using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeIpcClient(List<string> calls, bool available) : IIpcClient
{
    private readonly TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Result<CommandResult, IpcError> CommandReply { get; init; } = new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));

    public Result<RemoteRootSummaryView, IpcError> SummaryReply { get; init; } = new Ok<RemoteRootSummaryView>(new RemoteRootSummaryView(0));

    public Result<AccountsView, IpcError> AccountsReply { get; init; } = new Ok<AccountsView>(new AccountsView([]));

    public Func<GetRemoteFolders, Result<RemoteFoldersView, IpcError>> FoldersReply { get; init; } = _ => new Ok<RemoteFoldersView>(new RemoteFoldersView([]));

    public Result<SyncJobsView, IpcError> JobsReply { get; init; } = new Ok<SyncJobsView>(new SyncJobsView([]));

    public Func<GetSyncScopes, Result<SyncScopesView, IpcError>> ScopesReply { get; init; } = _ => new Ok<SyncScopesView>(new SyncScopesView([]));

    public Result<ActivitiesView, IpcError> ActivitiesReply { get; init; } = new Ok<ActivitiesView>(new ActivitiesView([]));

    public List<GetRemoteFolders> FolderQueries { get; } = [];

    private readonly List<Delegate> eventHandlers = [];

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
            GetAccounts => (Result<TResult, IpcError>)(object)AccountsReply,
            GetSyncJobs => (Result<TResult, IpcError>)(object)JobsReply,
            GetActivities => (Result<TResult, IpcError>)(object)ActivitiesReply,
            GetSyncScopes scopes => (Result<TResult, IpcError>)(object)ScopesReply(scopes),
            GetRemoteFolders folders => (Result<TResult, IpcError>)(object)Record(folders),
            _ => new Ok<TResult>((TResult)(object)Status)
        });
    }

    private Result<RemoteFoldersView, IpcError> Record(GetRemoteFolders query)
    {
        FolderQueries.Add(query);

        return FoldersReply(query);
    }

    public void OnEvent<TEvent>(Action<TEvent> handler)
        where TEvent : IEvent
        => eventHandlers.Add(handler);

    public void Raise<TEvent>(TEvent @event)
        where TEvent : IEvent
    {
        foreach (var handler in eventHandlers.OfType<Action<TEvent>>()) handler(@event);
    }

    public Task<Result<Unit, IpcError>> SubscribeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add("subscribe");

        return Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
