using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class ConflictResolutionShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task journal_every_decision()
    {
        var sink = new RecordingConflictEventSink();
        await using var host = CreateHost(sink);
        var job = await SeedConflictingItemAsync(host);

        await PlanAsync(host, job);
        await PlanAsync(host, job);

        var conflict = (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value.ShouldHaveSingleItem();
        var entry = (await host.Services.GetRequiredService<IEventJournal>().ListAsync(conflict.Id.Value, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldHaveSingleItem();
        entry.EventType.ShouldBe("ConflictDetected");
        entry.OccurredUtc.ShouldBe(conflict.CreatedUtc);
        entry.PayloadJson.ShouldContain("ModifyModify");
        entry.PayloadJson.ShouldContain(conflict.ItemId.Value.ToString());
        sink.Detected.Select(detected => detected.Id).ShouldBe([conflict.Id]);
    }

    [Fact]
    public async Task keep_entries_oldest_first_per_aggregate()
    {
        await using var host = CreateHost(new RecordingConflictEventSink());
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        var aggregate = Guid.NewGuid();
        var later = new JournalEntry(Guid.NewGuid(), Stamp.AddMinutes(1), "ConflictResolved", aggregate, "{}");
        var earlier = new JournalEntry(Guid.NewGuid(), Stamp, "ConflictDetected", aggregate, "{}");
        var other = new JournalEntry(Guid.NewGuid(), Stamp, "ConflictDetected", Guid.NewGuid(), "{}");

        await journal.AppendAsync(later, Token);
        await journal.AppendAsync(earlier, Token);
        await journal.AppendAsync(other, Token);
        var listed = await journal.ListAsync(aggregate, Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldBe([earlier, later]);
    }

    [Fact]
    public async Task honour_cancellation_when_appending_to_the_journal()
    {
        await using var host = CreateHost(new RecordingConflictEventSink());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var entry = new JournalEntry(Guid.NewGuid(), Stamp, "ConflictDetected", Guid.NewGuid(), "{}");

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IEventJournal>().AppendAsync(entry, cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_listing_the_journal()
    {
        await using var host = CreateHost(new RecordingConflictEventSink());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IEventJournal>().ListAsync(Guid.NewGuid(), cancelled.Token));
    }

    private static DatabaseTestHost CreateHost(RecordingConflictEventSink sink)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton(new ScriptedOperationExecutor(_ => new Ok<Unit>(default)) as IOperationExecutor);
            services.AddSingleton<IConflictEventSink>(sink);
            services.AddSyncEngine();
        });

    private static async Task<SyncJobId> SeedConflictingItemAsync(DatabaseTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", Root, BootstrapMode.Merge, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var remote = new ProviderItem(new RemoteItemId("r1"), new RelativePath("a.txt"), ItemType.File, 10, Stamp);
        (await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job.Id, Root), [remote], CrawlCheckpoint.None, Stamp, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var local = new LocalSnapshot(new RelativePath("a.txt"), 20, Stamp.AddHours(1), null, new ContentFingerprint("fp20"));
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job.Id, [local], [], Stamp.AddHours(2), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var item = (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value.Single().ItemId;
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        (await known.StageAsync(job.Id, operation, item, new KnownSnapshot(new RelativePath("a.txt"), 5, Stamp.AddDays(-1), null, null), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await known.CommitAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }

    private static async Task PlanAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token)).Value.ShouldBeOfType<Ok<PlanSummary>>();
}
