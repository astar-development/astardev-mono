using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class PlanBatchingShould
{
    private const int Files = 20;

    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task commit_a_plan_of_many_items_and_a_conflict_once()
    {
        var counter = new CommitCounter();
        await using var host = CreateHost(counter);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, [.. Enumerable.Range(0, Files).Select(index => new ProviderItem(new RemoteItemId($"r{index}"), new RelativePath($"file{index}.txt"), ItemType.File, 10, Stamp))]);
        var conflicted = await AddConflictAsync(host, job);
        counter.Reset();

        var summary = await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token);

        var commits = counter.Commits;
        summary.Value.ShouldBeOfType<Ok<PlanSummary>>().Value.ShouldBe(new PlanSummary(Files, 1));
        commits.ShouldBe(1);
        var conflict = (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value.ShouldHaveSingleItem();
        conflict.ItemId.ShouldBe(conflicted);
        var journaled = (await host.Services.GetRequiredService<IEventJournal>().ListAsync(conflict.Id.Value, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value;
        journaled.ShouldHaveSingleItem().EventType.ShouldBe("ConflictDetected");
    }

    [Fact]
    public async Task commit_once_per_batch_of_five_hundred_items()
    {
        var counter = new CommitCounter();
        await using var host = CreateHost(counter);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, [.. Enumerable.Range(0, 501).Select(index => new ProviderItem(new RemoteItemId($"r{index}"), new RelativePath($"file{index}.txt"), ItemType.File, 10, Stamp))]);
        counter.Reset();

        var summary = await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token);

        summary.Value.ShouldBeOfType<Ok<PlanSummary>>().Value.ShouldBe(new PlanSummary(501, 0));
        counter.Commits.ShouldBe(2);
    }

    [Fact]
    public async Task write_nothing_when_planning_again_finds_no_new_work()
    {
        var counter = new CommitCounter();
        await using var host = CreateHost(counter);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, [new ProviderItem(new RemoteItemId("r1"), new RelativePath("a.txt"), ItemType.File, 10, Stamp)]);
        await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token);
        counter.Reset();

        var summary = await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token);

        summary.Value.ShouldBeOfType<Ok<PlanSummary>>().Value.ShouldBe(new PlanSummary(0, 0));
        counter.Commits.ShouldBe(0);
    }

    private static DatabaseTestHost CreateHost(CommitCounter counter)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton<IOperationExecutor>(new ScriptedOperationExecutor(_ => new Ok<Unit>(default)));
            services.AddSyncEngine();
            services.ConfigureDbContext<CloudSyncDbContext>(options => options.AddInterceptors(counter));
        });

    private static async Task<SyncJobId> SeedAsync(DatabaseTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", Root, BootstrapMode.Merge, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }

    private static async Task AddRemoteAsync(DatabaseTestHost host, SyncJobId job, IReadOnlyList<ProviderItem> items)
        => (await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job, Root), items, CrawlCheckpoint.None, Stamp, Token)).Value.ShouldBeOfType<Ok<Unit>>();

    private static async Task<ItemId> AddConflictAsync(DatabaseTestHost host, SyncJobId job)
    {
        await AddRemoteAsync(host, job, [new ProviderItem(new RemoteItemId("conflicted"), new RelativePath("conflicted.txt"), ItemType.File, 10, Stamp)]);
        var snapshot = new LocalSnapshot(new RelativePath("conflicted.txt"), 20, Stamp.AddHours(1), null, new ContentFingerprint("fp20"));
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [snapshot], [], Stamp.AddHours(2), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var items = (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;
        var item = items.Single(stored => stored.Path == new RelativePath("conflicted.txt")).ItemId;
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        (await known.StageAsync(job, operation, item, new KnownSnapshot(new RelativePath("conflicted.txt"), 5, Stamp.AddDays(-1), null, null), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await known.CommitAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return item;
    }
}
