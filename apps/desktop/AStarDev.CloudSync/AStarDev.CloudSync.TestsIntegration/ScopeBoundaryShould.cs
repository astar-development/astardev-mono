using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class ScopeBoundaryShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId DocsId = new("docs-id");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task queue_no_upload_for_a_local_file_outside_every_scope()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await RecordLocalAsync(host, job, "stray/a.txt");

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(0, 0));
        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task queue_an_upload_for_a_local_file_inside_a_scope()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await RecordLocalAsync(host, job, "docs/a.txt");

        await PlanAsync(host, job);

        (await Operations(host)).ShouldHaveSingleItem().Type.ShouldBe(OperationType.UploadFile);
    }

    [Fact]
    public async Task queue_nothing_while_the_job_has_no_scope()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await RecordLocalAsync(host, job, "docs/a.txt");
        (await host.Services.GetRequiredService<ISyncScopeStore>().RemoveAsync(new SyncScope(job, DocsId), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await PlanAsync(host, job);

        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task plan_no_local_delete_for_a_file_outside_every_scope_after_a_restart()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await RecordLocalAsync(host, job, "stray/a.txt");
        await CommitKnownAsync(host, job, "stray/a.txt");

        await host.RestartAsync();
        await PlanAsync(host, job);

        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task plan_a_local_delete_for_a_file_inside_a_scope()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await RecordLocalAsync(host, job, "docs/a.txt");
        await CommitKnownAsync(host, job, "docs/a.txt");

        await PlanAsync(host, job);

        (await Operations(host)).ShouldHaveSingleItem().Type.ShouldBe(OperationType.DeleteLocal);
    }

    [Fact]
    public async Task plan_no_delete_once_the_scope_is_removed()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await RecordLocalAsync(host, job, "docs/a.txt");
        await CommitKnownAsync(host, job, "docs/a.txt");
        (await host.Services.GetRequiredService<ISyncScopeStore>().RemoveAsync(new SyncScope(job, DocsId), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await PlanAsync(host, job);

        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task keep_the_scope_path_across_a_restart()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);

        await host.RestartAsync();
        var scopes = (await host.Services.GetRequiredService<ISyncScopeStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value;

        scopes.ShouldBe([new SyncScope(job, DocsId, new RelativePath("docs"))]);
    }

    [Fact]
    public async Task honour_cancellation_when_reading_the_scopes()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ISyncScopeStore>().ListAsync(job, cancelled.Token));
    }

    private static DatabaseTestHost CreateHost()
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton<IOperationExecutor>(new ScriptedOperationExecutor(_ => new Ok<Unit>(default)));
            services.AddSyncEngine();
        });

    private static async Task<SyncJobId> SeedAsync(DatabaseTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Merge, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, DocsId, new RelativePath("docs")), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }

    private static async Task RecordLocalAsync(DatabaseTestHost host, SyncJobId job, string path)
    {
        var snapshot = new LocalSnapshot(new RelativePath(path), 10, Stamp.AddHours(1), null, new ContentFingerprint("fp10"));
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [snapshot], [], Stamp.AddHours(2), Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task CommitKnownAsync(DatabaseTestHost host, SyncJobId job, string path)
    {
        var item = (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value.Single(row => row.Path.Value == path).ItemId;
        var store = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        (await store.StageAsync(job, operation, item, new KnownSnapshot(new RelativePath(path), 10, Stamp.AddHours(1), null, new ContentFingerprint("fp10")), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await store.CommitAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task<PlanSummary> PlanAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token)).Value.ShouldBeOfType<Ok<PlanSummary>>().Value;

    private static async Task<IReadOnlyList<Operation>> Operations(DatabaseTestHost host)
        => (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;
}
