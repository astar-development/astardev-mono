using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class PlanStoreShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task queue_the_operations_and_stage_the_baseline_against_the_last_one()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await AddJobAsync(host);
        var item = new ItemId(Guid.NewGuid());
        var first = NewOperation(job, item, 0);
        var last = NewOperation(job, item, 1);
        var target = new KnownSnapshot(new RelativePath("a.txt"), 10, Stamp, null, null);

        var saved = await Store(host).SaveAsync(new PlanBatch(job, [], [new PlannedWork([first, last], target)], []), Token);

        saved.Value.ShouldBeOfType<Ok<IReadOnlyList<ConflictId>>>().Value.ShouldBeEmpty();
        (await Operations(host)).Select(operation => operation.Id).ShouldBe([first.Id, last.Id]);
        (await host.Services.GetRequiredService<IKnownSnapshotStore>().CommitAsync(last.Id, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await Known(host, job)).ShouldHaveSingleItem().ShouldBe(new KnownEntry(item, target));
    }

    [Fact]
    public async Task store_a_conflict_with_its_journal_entry_only_once()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await AddJobAsync(host);
        var item = new ItemId(Guid.NewGuid());
        var first = NewConflict(job, item);
        var again = NewConflict(job, item);

        var stored = await Store(host).SaveAsync(new PlanBatch(job, [], [], [first]), Token);
        var skipped = await Store(host).SaveAsync(new PlanBatch(job, [], [], [again]), Token);

        stored.Value.ShouldBeOfType<Ok<IReadOnlyList<ConflictId>>>().Value.ShouldBe([first.Conflict.Id]);
        skipped.Value.ShouldBeOfType<Ok<IReadOnlyList<ConflictId>>>().Value.ShouldBeEmpty();
        (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value.ShouldHaveSingleItem().Id.ShouldBe(first.Conflict.Id);
        (await Journal(host, first)).ShouldHaveSingleItem().ShouldBe(first.Detected);
        (await Journal(host, again)).ShouldBeEmpty();
    }

    [Fact]
    public async Task forget_the_baseline_of_an_item_gone_from_both_sides()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await AddJobAsync(host);
        var item = new ItemId(Guid.NewGuid());
        var operation = new OperationId(Guid.NewGuid());
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();
        await known.StageAsync(job, operation, item, new KnownSnapshot(new RelativePath("a.txt"), 10, Stamp, null, null), Token);
        await known.CommitAsync(operation, Token);

        var saved = await Store(host).SaveAsync(new PlanBatch(job, [item], [], []), Token);

        saved.Value.ShouldBeOfType<Ok<IReadOnlyList<ConflictId>>>();
        (await Known(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task write_nothing_of_a_batch_for_a_job_that_is_not_stored()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = new SyncJobId(Guid.NewGuid());
        var batch = new PlanBatch(job, [], [new PlannedWork([NewOperation(job, new ItemId(Guid.NewGuid()), 0)], null)], [NewConflict(job, new ItemId(Guid.NewGuid()))]);

        var saved = await Store(host).SaveAsync(batch, Token);

        saved.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new SyncJobNotFound(job));
        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task roll_back_the_whole_batch_when_one_write_fails()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await AddJobAsync(host);
        var queued = NewOperation(job, new ItemId(Guid.NewGuid()), 0);
        var duplicate = NewOperation(job, new ItemId(Guid.NewGuid()), 1) with { Id = queued.Id };
        var conflict = NewConflict(job, new ItemId(Guid.NewGuid()));
        var batch = new PlanBatch(job, [], [new PlannedWork([queued], null), new PlannedWork([duplicate], null)], [conflict]);

        var saved = await Store(host).SaveAsync(batch, Token);

        saved.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBeOfType<StorageFailed>();
        (await Operations(host)).ShouldBeEmpty();
        (await Journal(host, conflict)).ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await AddJobAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var batch = new PlanBatch(job, [], [new PlannedWork([NewOperation(job, new ItemId(Guid.NewGuid()), 0)], null)], []);

        await Should.ThrowAsync<OperationCanceledException>(() => Store(host).SaveAsync(batch, cancelled.Token));
        (await Operations(host)).ShouldBeEmpty();
    }

    private static IPlanStore Store(PersistenceTestHost host) => host.Services.GetRequiredService<IPlanStore>();

    private static async Task<SyncJobId> AddJobAsync(PersistenceTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Merge, true, Stamp, Stamp);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }

    private static Operation NewOperation(SyncJobId job, ItemId item, int rank)
        => new(new OperationId(Guid.NewGuid()), job, item, OperationType.DownloadFile, OperationPriority.Background, OperationStatus.Pending, 0, Stamp, null, Stamp.AddTicks(rank), null);

    private static PlannedConflict NewConflict(SyncJobId job, ItemId item)
    {
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), job, item, ConflictType.ModifyModify, ConflictStatus.Open, Stamp);

        return new PlannedConflict(conflict, new JournalEntry(Guid.NewGuid(), Stamp, "ConflictDetected", conflict.Id.Value, "{}"));
    }

    private static async Task<IReadOnlyList<Operation>> Operations(PersistenceTestHost host)
        => (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

    private static async Task<IReadOnlyList<KnownEntry>> Known(PersistenceTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<IKnownSnapshotStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<KnownEntry>>>().Value;

    private static async Task<IReadOnlyList<JournalEntry>> Journal(PersistenceTestHost host, PlannedConflict conflict)
        => (await host.Services.GetRequiredService<IEventJournal>().ListAsync(conflict.Conflict.Id.Value, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value;
}
