using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class QueueQueryShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(DaemonTestHost Host, SyncJob Job, IReadOnlyDictionary<string, ItemId> Items)> SeedAsync(RecordingIpcServer? server = null)
    {
        var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(new StubCloudProvider()).RemoveAll<IIpcServer>().AddSingleton<IIpcServer>(server ?? new RecordingIpcServer()));
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-queue", Token);
        var local = new[] { "Reports/a.txt", "Photos/b.jpg", "Docs/c.md" }.Select(path => new LocalSnapshot(new RelativePath(path), 42, Stamp, null, null)).ToList();
        await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job.Id, local, [], Stamp, Token);
        var stored = (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;

        return (host, job, stored.ToDictionary(item => item.Path.Value, item => item.ItemId));
    }

    private static async Task<Operation> AddAsync(DaemonTestHost host, SyncJob job, ItemId item, OperationType type, OperationStatus status, DateTimeOffset createdUtc)
    {
        var operation = new Operation(new OperationId(Guid.NewGuid()), job.Id, item, type, OperationPriority.Background, OperationStatus.Pending, 0, DateTimeOffset.UnixEpoch, null, createdUtc);
        var store = host.Services.GetRequiredService<IOperationStore>();
        await store.EnqueueAsync(operation, Token);
        if (status == OperationStatus.Pending) return operation;

        await store.ClaimNextAsync(DateTimeOffset.UtcNow, 100, Token);
        if (status == OperationStatus.Completed) await store.CompleteAsync(operation.Id, Token);
        if (status == OperationStatus.DeadLetter) await store.FailAsync(operation.Id, "forbidden", null, Token);

        return operation;
    }

    private static async Task<QueueView> QueryAsync(DaemonTestHost host)
    {
        var result = await host.Services.GetRequiredService<QueueQuery>().HandleAsync(new GetQueue(), Token);

        return result.Value.ShouldBeOfType<Ok<QueueView>>().Value;
    }

    [Fact]
    public async Task list_only_the_newest_operations_but_count_them_all()
    {
        var (host, job, items) = await SeedAsync();
        await using var _ = host;
        var total = ApplicationConstants.QueueListLimit + 5;
        for (var index = 0; index < total; index++) await AddAsync(host, job, items["Docs/c.md"], OperationType.DownloadFile, OperationStatus.Pending, Stamp.AddSeconds(index));

        var queue = await QueryAsync(host);

        queue.Pending.ShouldBe(total);
        queue.Operations.Count.ShouldBe(ApplicationConstants.QueueListLimit);
        queue.Operations[0].QueuedUtc.ShouldBe(Stamp.AddSeconds(total - 1));
    }

    [Fact]
    public async Task list_pending_and_running_operations_newest_first_with_provider_path_and_size()
    {
        var (host, job, items) = await SeedAsync();
        await using var _ = host;
        await AddAsync(host, job, items["Docs/c.md"], OperationType.DeleteLocal, OperationStatus.Completed, Stamp.AddDays(-1));
        var oldest = await AddAsync(host, job, items["Reports/a.txt"], OperationType.UploadFile, OperationStatus.Running, Stamp.AddDays(-2));
        var newest = await AddAsync(host, job, items["Photos/b.jpg"], OperationType.DownloadFile, OperationStatus.Pending, Stamp);

        var queue = await QueryAsync(host);

        queue.Operations.Select(operation => operation.OperationId).ShouldBe([newest.Id.Value, oldest.Id.Value]);
        queue.Operations[0].ShouldBe(new QueuedOperationView(newest.Id.Value, "OneDrive", "DownloadFile", "Pending", "Photos/b.jpg", 42, Stamp));
        queue.Operations[1].Status.ShouldBe("Running");
    }

    [Fact]
    public async Task count_every_status_and_fold_dead_letters_into_failed()
    {
        var (host, job, items) = await SeedAsync();
        await using var _ = host;
        await AddAsync(host, job, items["Reports/a.txt"], OperationType.UploadFile, OperationStatus.Completed, Stamp);
        await AddAsync(host, job, items["Photos/b.jpg"], OperationType.UploadFile, OperationStatus.DeadLetter, Stamp);
        await AddAsync(host, job, items["Docs/c.md"], OperationType.UploadFile, OperationStatus.Pending, Stamp);
        await AddAsync(host, job, items["Docs/c.md"], OperationType.RenameLocal, OperationStatus.Pending, Stamp);

        var queue = await QueryAsync(host);

        (queue.Pending, queue.Running, queue.Completed, queue.Failed).ShouldBe((2, 0, 1, 1));
    }

    [Fact]
    public async Task return_an_empty_queue_when_nothing_is_queued()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var queue = await QueryAsync(host);

        (queue.Pending, queue.Running, queue.Completed, queue.Failed).ShouldBe((0, 0, 0, 0));
        queue.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task publish_a_change_only_when_the_queue_moves()
    {
        var server = new RecordingIpcServer();
        var (host, job, items) = await SeedAsync(server);
        await using var _ = host;
        var query = host.Services.GetRequiredService<QueueQuery>();

        await query.PublishIfChangedAsync(Token);
        await query.PublishIfChangedAsync(Token);
        await AddAsync(host, job, items["Reports/a.txt"], OperationType.UploadFile, OperationStatus.Pending, Stamp);
        await query.PublishIfChangedAsync(Token);

        server.Published.OfType<QueueChanged>().Select(changed => changed.Queue.Pending).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task publish_when_a_dead_letter_is_retried_or_ignored()
    {
        var server = new RecordingIpcServer();
        var (host, job, items) = await SeedAsync(server);
        await using var _ = host;
        var retried = await AddAsync(host, job, items["Reports/a.txt"], OperationType.UploadFile, OperationStatus.DeadLetter, Stamp);
        var ignored = await AddAsync(host, job, items["Photos/b.jpg"], OperationType.UploadFile, OperationStatus.DeadLetter, Stamp);
        await host.Services.GetRequiredService<QueueQuery>().PublishIfChangedAsync(Token);

        await host.Services.GetRequiredService<RetryOperationHandler>().HandleAsync(new RetryOperation(retried.Id.Value), Token);
        await host.Services.GetRequiredService<IgnoreOperationHandler>().HandleAsync(new IgnoreOperation(ignored.Id.Value), Token);

        server.Published.OfType<QueueChanged>().Select(changed => (changed.Queue.Pending, changed.Queue.Failed)).ShouldBe([(0, 2), (1, 1), (1, 0)]);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var query = host.Services.GetRequiredService<QueueQuery>();

        await Should.ThrowAsync<OperationCanceledException>(() => query.HandleAsync(new GetQueue(), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => query.PublishIfChangedAsync(cancelled.Token));
    }
}
