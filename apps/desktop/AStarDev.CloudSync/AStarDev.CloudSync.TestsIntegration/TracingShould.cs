using System.Diagnostics;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class TracingShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task link_planning_queue_execution_and_commit_in_one_trace()
    {
        using var spans = new RecordedSpans();
        var executor = new ScriptedOperationExecutor(_ => new Ok<Unit>(default));
        await using var host = CreateHost(executor);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt");
        await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token);
        await host.RestartAsync();

        await using var scheduler = host.Services.GetRequiredService<OperationScheduler>();
        await scheduler.RunOnceAsync(Token);

        var recorded = spans.ForJob(job);
        var item = Single(recorded, "cloudsync.item.plan");
        var planning = Single(recorded, "cloudsync.operation.plan");
        var publish = Single(recorded, "operations publish");
        var process = Single(recorded, "operations process");
        var commit = Single(recorded, "cloudsync.snapshot.commit");
        new[] { planning, publish, process, commit }.ShouldAllBe(span => span.TraceId == item.TraceId);
        planning.ParentSpanId.ShouldBe(item.SpanId);
        publish.ParentSpanId.ShouldBe(planning.SpanId);
        process.ParentSpanId.ShouldBe(planning.SpanId);
        commit.ParentSpanId.ShouldBe(process.SpanId);
        publish.Kind.ShouldBe(ActivityKind.Producer);
        process.Kind.ShouldBe(ActivityKind.Consumer);
        process.Status.ShouldNotBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task mark_a_failed_operation_span_as_an_error()
    {
        using var spans = new RecordedSpans();
        var executor = new ScriptedOperationExecutor(_ => new Fail<ProviderError>(new Permanent("denied")));
        await using var host = CreateHost(executor);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt");
        await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token);

        await using var scheduler = host.Services.GetRequiredService<OperationScheduler>();
        await scheduler.RunOnceAsync(Token);

        var recorded = spans.ForJob(job);
        var process = Single(recorded, "operations process");
        process.Status.ShouldBe(ActivityStatusCode.Error);
        process.StatusDescription!.ShouldContain("denied");
        process.GetTagItem("error.type").ShouldBe("Permanent");
        recorded.Where(span => span.OperationName == "cloudsync.snapshot.commit").ShouldBeEmpty();
        Single(recorded, "cloudsync.operation.plan").Status.ShouldNotBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task span_delta_processing()
    {
        using var spans = new RecordedSpans();
        var provider = new FakeCloudProvider(pageSize: 100);
        provider.AddItem(new ProviderItem(new RemoteItemId("a"), new RelativePath("a.txt"), ItemType.File, 1, Stamp), Root);
        await using var host = DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<ICloudProvider>(provider);
            services.AddSingleton<ICrawlProgressSink>(new RecordingProgressSink());
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSyncEngine();
        });
        var job = await SeedAsync(host);
        (await host.Services.GetRequiredService<InitialCrawler>().CrawlAsync(job, Token)).Value.ShouldBeOfType<Ok<CrawlSummary>>();
        provider.ScriptChanges(Root, new ProviderChange(ChangeKind.Deleted, new RemoteItemId("a")));

        (await host.Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(new SyncScope(job, Root), Token)).Value.ShouldBeOfType<Ok<int>>();

        var recorded = spans.ForJob(job);
        var scope = Single(recorded, "cloudsync.delta.process");
        var page = Single(recorded, "cloudsync.delta.page");
        page.ParentSpanId.ShouldBe(scope.SpanId);
        page.TraceId.ShouldBe(scope.TraceId);
        scope.GetTagItem("cloudsync.delta.changes").ShouldBe(1);
        scope.Status.ShouldNotBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task start_a_new_trace_for_an_operation_queued_without_a_trace_parent()
    {
        using var spans = new RecordedSpans();
        await using var host = CreateHost(new ScriptedOperationExecutor(_ => new Ok<Unit>(default)));
        var job = await SeedAsync(host);
        var operation = new Operation(new OperationId(Guid.NewGuid()), job, new ItemId(Guid.NewGuid()), OperationType.UploadFile, OperationPriority.Background, OperationStatus.Pending, 0, Stamp, null, Stamp);
        await host.Services.GetRequiredService<IOperationStore>().EnqueueAsync(operation, Token);

        await using var scheduler = host.Services.GetRequiredService<OperationScheduler>();
        await scheduler.RunOnceAsync(Token);

        var process = Single(spans.ForJob(job), "operations process");
        process.ParentSpanId.ToString().ShouldBe(default(ActivitySpanId).ToString());
    }

    [Fact]
    public async Task honour_cancellation_when_planning()
    {
        await using var host = CreateHost(new ScriptedOperationExecutor(_ => new Ok<Unit>(default)));
        var job = await SeedAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, cancelled.Token));
    }

    private static Activity Single(IReadOnlyList<Activity> spans, string name) => spans.Where(span => span.OperationName == name).ShouldHaveSingleItem();

    private static DatabaseTestHost CreateHost(IOperationExecutor executor)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton(executor);
            services.AddSyncEngine();
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

    private static async Task AddRemoteAsync(DatabaseTestHost host, SyncJobId job, string id, string path)
    {
        var item = new ProviderItem(new RemoteItemId(id), new RelativePath(path), ItemType.File, 10, Stamp);
        (await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job, Root), [item], CrawlCheckpoint.None, Stamp, Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }
}
