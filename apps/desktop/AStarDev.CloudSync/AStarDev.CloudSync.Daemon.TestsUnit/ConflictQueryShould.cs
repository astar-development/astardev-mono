using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ConflictQueryShould : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);

    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-conflicts-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task list_open_conflicts()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var store = host.Services.GetRequiredService<IConflictStore>();
        var older = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.ModifyModify, ConflictStatus.Open, Stamp);
        var newer = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.KeepDelete, ConflictStatus.Open, Stamp.AddMinutes(5));
        var resolved = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.RenameRename, ConflictStatus.Resolved, Stamp.AddMinutes(1));
        await store.AddAsync(newer, Token);
        await store.AddAsync(resolved, Token);
        await store.AddAsync(older, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var view = (await client.QueryAsync(new GetConflicts(job.Id.Value), Token)).Value.ShouldBeOfType<Ok<ConflictsView>>().Value;
        await host.StopAsync(Token);

        view.Conflicts.ShouldBe(
        [
            new ConflictView(older.Id.Value, job.Id.Value, older.ItemId.Value, "ModifyModify", Stamp),
            new ConflictView(newer.Id.Value, job.Id.Value, newer.ItemId.Value, "KeepDelete", Stamp.AddMinutes(5))
        ]);
    }

    [Fact]
    public async Task list_nothing_for_an_unknown_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<ConflictsQuery>().HandleAsync(new GetConflicts(Guid.NewGuid()), Token);

        result.Value.ShouldBeOfType<Ok<ConflictsView>>().Value.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task publish_conflict_detected_and_conflict_resolved_to_a_subscriber()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.CreateCreate, ConflictStatus.Open, Stamp);
        var detected = new TaskCompletionSource<ConflictDetected>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolved = new TaskCompletionSource<ConflictResolved>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        client.OnEvent<ConflictDetected>(@event => detected.TrySetResult(@event));
        client.OnEvent<ConflictResolved>(@event => resolved.TrySetResult(@event));

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await client.SubscribeAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var sink = host.Services.GetRequiredService<IConflictEventSink>();
        await sink.DetectedAsync(conflict, Token);
        await sink.ResolvedAsync(conflict with { Status = ConflictStatus.Resolved }, "KeepLocal", Stamp.AddMinutes(2), Token);
        var detectedEvent = await detected.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        var resolvedEvent = await resolved.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await host.StopAsync(Token);

        detectedEvent.Conflict.ShouldBe(new ConflictView(conflict.Id.Value, job.Id.Value, conflict.ItemId.Value, "CreateCreate", Stamp));
        resolvedEvent.ShouldBe(new ConflictResolved(conflict.Id.Value, job.Id.Value, conflict.ItemId.Value, "KeepLocal", Stamp.AddMinutes(2)));
    }

    [Fact]
    public async Task register_the_ipc_sink_for_conflict_events()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        host.Services.GetRequiredService<IConflictEventSink>().ShouldBeOfType<IpcConflictEventSink>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ConflictsQuery>().HandleAsync(new GetConflicts(Guid.NewGuid()), cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_publishing_an_event()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), new SyncJobId(Guid.NewGuid()), new ItemId(Guid.NewGuid()), ConflictType.CreateCreate, ConflictStatus.Open, Stamp);

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IConflictEventSink>().DetectedAsync(conflict, cancelled.Token));
    }

    private async Task<SyncJob> NewJobAsync(DaemonTestHost host)
    {
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);

        return await host.AddJobAsync(account, localRoot, Token);
    }
}
