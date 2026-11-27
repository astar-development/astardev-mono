using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class HistoryQueryShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(DaemonTestHost Host, SyncJob Job, IReadOnlyDictionary<string, ItemId> Items)> SeedAsync()
    {
        var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-history", Token);
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

    private static async Task<IReadOnlyList<HistoryEntryView>> QueryAsync(DaemonTestHost host, GetHistory query)
    {
        var result = await host.Services.GetRequiredService<HistoryQuery>().HandleAsync(query, Token);

        return result.Value.ShouldBeOfType<Ok<HistoryView>>().Value.Entries;
    }

    [Fact]
    public async Task list_completed_and_failed_operations_newest_first()
    {
        var (host, job, items) = await SeedAsync();
        await using var _ = host;
        var oldest = await AddAsync(host, job, items["Reports/a.txt"], OperationType.UploadFile, OperationStatus.Completed, Stamp.AddDays(-2));
        var newest = await AddAsync(host, job, items["Photos/b.jpg"], OperationType.DownloadFile, OperationStatus.DeadLetter, Stamp);
        var middle = await AddAsync(host, job, items["Docs/c.md"], OperationType.DeleteLocal, OperationStatus.Completed, Stamp.AddDays(-1));
        await AddAsync(host, job, items["Reports/a.txt"], OperationType.RenameLocal, OperationStatus.Pending, Stamp.AddDays(1));

        var entries = await QueryAsync(host, new GetHistory());

        entries.Select(entry => entry.OperationId).ShouldBe([newest.Id.Value, middle.Id.Value, oldest.Id.Value]);
        entries.Select(entry => entry.Status).ShouldBe(["Failed", "Completed", "Completed"]);
        entries[0].ShouldBe(new HistoryEntryView(newest.Id.Value, "OneDrive", "DownloadFile", "Failed", Stamp, "Photos/b.jpg", 42));
    }

    [Fact]
    public async Task list_only_the_newest_operations_so_the_answer_fits_an_ipc_frame()
    {
        var (host, job, items) = await SeedAsync();
        await using var _ = host;
        var total = ApplicationConstants.HistoryListLimit + 5;
        for (var index = 0; index < total; index++) await AddAsync(host, job, items["Docs/c.md"], OperationType.DownloadFile, OperationStatus.Completed, Stamp.AddSeconds(index));

        var entries = await QueryAsync(host, new GetHistory());

        entries.Count.ShouldBe(ApplicationConstants.HistoryListLimit);
        entries[0].Timestamp.ShouldBe(Stamp.AddSeconds(total - 1));
    }

    [Fact]
    public async Task export_every_operation_without_the_screen_limit()
    {
        var (host, job, items) = await SeedAsync();
        await using var _ = host;
        var total = ApplicationConstants.HistoryListLimit + 5;
        for (var index = 0; index < total; index++) await AddAsync(host, job, items["Docs/c.md"], OperationType.DownloadFile, OperationStatus.Completed, Stamp.AddSeconds(index));

        var result = await host.Services.GetRequiredService<ExportQuery>().HandleAsync(new ExportHistory(new GetHistory()), Token);

        result.Value.ShouldBeOfType<Ok<ExportView>>().Value.Csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(total + 1);
    }

    [Fact]
    public async Task filter_by_status_and_date_range()
    {
        var (host, job, items) = await SeedAsync();
        await using var _ = host;
        await AddAsync(host, job, items["Reports/a.txt"], OperationType.UploadFile, OperationStatus.Completed, Stamp.AddDays(-10));
        var inRange = await AddAsync(host, job, items["Photos/b.jpg"], OperationType.UploadFile, OperationStatus.DeadLetter, Stamp.AddDays(-3));
        await AddAsync(host, job, items["Docs/c.md"], OperationType.UploadFile, OperationStatus.Completed, Stamp.AddDays(-3));
        await AddAsync(host, job, items["Docs/c.md"], OperationType.DeleteRemote, OperationStatus.DeadLetter, Stamp);

        var failed = await QueryAsync(host, new GetHistory(Status: "failed"));
        var narrowed = await QueryAsync(host, new GetHistory(Status: "Failed", From: Stamp.AddDays(-5), To: Stamp.AddDays(-1)));
        var byType = await QueryAsync(host, new GetHistory(Type: "DeleteRemote"));
        var byProvider = await QueryAsync(host, new GetHistory(Provider: "Dropbox"));

        failed.Count.ShouldBe(2);
        narrowed.Select(entry => entry.OperationId).ShouldBe([inRange.Id.Value]);
        byType.ShouldHaveSingleItem().Type.ShouldBe("DeleteRemote");
        byProvider.ShouldBeEmpty();
    }

    [Fact]
    public async Task filter_by_path()
    {
        var (host, job, items) = await SeedAsync();
        await using var _ = host;
        await AddAsync(host, job, items["Reports/a.txt"], OperationType.UploadFile, OperationStatus.Completed, Stamp);
        await AddAsync(host, job, items["Photos/b.jpg"], OperationType.UploadFile, OperationStatus.Completed, Stamp.AddMinutes(-1));

        var matching = await QueryAsync(host, new GetHistory(Path: "photos"));
        var blank = await QueryAsync(host, new GetHistory(Path: " "));

        matching.ShouldHaveSingleItem().Path.ShouldBe("Photos/b.jpg");
        blank.Count.ShouldBe(2);
    }

    [Fact]
    public async Task return_an_empty_list_when_nothing_has_finished()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        (await QueryAsync(host, new GetHistory())).ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<HistoryQuery>().HandleAsync(new GetHistory(), cancelled.Token));
    }
}
