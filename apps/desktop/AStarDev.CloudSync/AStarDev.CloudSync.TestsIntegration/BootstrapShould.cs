using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class BootstrapShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task download_mode_makes_remote_authoritative()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host, BootstrapMode.Download);
        await AddRemoteAsync(host, job, "r1", "differs.txt", 10);
        await AddRemoteAsync(host, job, "r2", "remote-only.txt", 4);
        await RecordLocalAsync(host, job, "differs.txt", 20);
        await RecordLocalAsync(host, job, "local-only.txt", 7);

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(2, 0));
        var operations = await Operations(host);
        operations.ShouldAllBe(operation => operation.Type == OperationType.DownloadFile);
        (await ItemPaths(host, job, operations)).ShouldBe(["differs.txt", "remote-only.txt"], ignoreOrder: true);
        (await Conflicts(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task upload_mode_makes_local_authoritative()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host, BootstrapMode.Upload);
        await AddRemoteAsync(host, job, "r1", "differs.txt", 10);
        await AddRemoteAsync(host, job, "r2", "remote-only.txt", 4);
        await RecordLocalAsync(host, job, "differs.txt", 20);
        await RecordLocalAsync(host, job, "local-only.txt", 7);

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(2, 0));
        var operations = await Operations(host);
        operations.ShouldAllBe(operation => operation.Type == OperationType.UploadFile);
        (await ItemPaths(host, job, operations)).ShouldBe(["differs.txt", "local-only.txt"], ignoreOrder: true);
        (await Conflicts(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task merge_mode_creates_a_conflict_when_both_sides_differ()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host, BootstrapMode.Merge);
        await AddRemoteAsync(host, job, "r1", "differs.txt", 10);
        await RecordLocalAsync(host, job, "differs.txt", 20);
        await RecordLocalAsync(host, job, "local-only.txt", 7);

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(1, 1));
        var conflict = (await Conflicts(host, job)).ShouldHaveSingleItem();
        conflict.Type.ShouldBe(ConflictType.CreateCreate);
        var operation = (await Operations(host)).ShouldHaveSingleItem();
        operation.Type.ShouldBe(OperationType.UploadFile);
    }

    [Fact]
    public async Task apply_the_mode_to_the_first_plan_only()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host, BootstrapMode.Download);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);
        await PlanAsync(host, job);
        await RecordLocalAsync(host, job, "later.txt", 3);

        var second = await PlanAsync(host, job);

        second.ShouldBe(new PlanSummary(1, 0));
        (await Operations(host)).Select(operation => operation.Type).ShouldBe([OperationType.DownloadFile, OperationType.UploadFile]);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host, BootstrapMode.Download);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, cancelled.Token));
    }

    private static DatabaseTestHost CreateHost()
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton<IOperationExecutor>(new ScriptedOperationExecutor(_ => new Ok<Unit>(default)));
            services.AddSyncEngine();
        });

    private static async Task<SyncJobId> SeedAsync(DatabaseTestHost host, BootstrapMode mode)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", Root, mode, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }

    private static async Task AddRemoteAsync(DatabaseTestHost host, SyncJobId job, string id, string path, long size)
    {
        var item = new ProviderItem(new RemoteItemId(id), new RelativePath(path), ItemType.File, size, Stamp);
        var saved = await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job, Root), [item], CrawlCheckpoint.None, Stamp, Token);
        saved.Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task RecordLocalAsync(DatabaseTestHost host, SyncJobId job, string path, long size)
    {
        var snapshot = new LocalSnapshot(new RelativePath(path), size, Stamp.AddHours(1), null, new ContentFingerprint("fp" + size));
        var recorded = await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [snapshot], [], Stamp.AddHours(2), Token);
        recorded.Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task<PlanSummary> PlanAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token)).Value.ShouldBeOfType<Ok<PlanSummary>>().Value;

    private static async Task<IReadOnlyList<Operation>> Operations(DatabaseTestHost host)
        => (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

    private static async Task<IReadOnlyList<Conflict>> Conflicts(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value;

    private static async Task<IReadOnlyList<string>> ItemPaths(DatabaseTestHost host, SyncJobId job, IReadOnlyList<Operation> operations)
    {
        var items = (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;

        return operations.Select(operation => items.First(item => item.ItemId == operation.ItemId).Path.Value).ToList();
    }
}
