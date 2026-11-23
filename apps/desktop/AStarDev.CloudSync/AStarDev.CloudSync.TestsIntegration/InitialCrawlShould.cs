using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.FakeProvider;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using AStarDev.CloudSync.Persistence;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class InitialCrawlShould
{
    private static readonly RemoteItemId Docs = new("docs");
    private static readonly RemoteItemId Notes = new("notes");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task index_every_item_in_the_scope()
    {
        var provider = new FakeCloudProvider(pageSize: 100);
        provider.GenerateTree(1050, Docs);
        await using var host = CreateHost(provider, new RecordingProgressSink());
        var job = await SeedAsync(host, Docs);

        var summary = await CrawlAsync(host, job);

        summary.ShouldBe(new CrawlSummary(1, 0));
        Count(host, "Items").ShouldBe(1050);
        Count(host, "ItemSnapshots").ShouldBe(1050);
        provider.PagesServed.ShouldBe(11);
    }

    [Fact]
    public async Task resume_from_the_checkpoint_without_reindexing()
    {
        var provider = new FakeCloudProvider(pageSize: 100);
        provider.GenerateTree(1000, Docs);
        await using var host = CreateHost(provider, new RecordingProgressSink());
        var job = await SeedAsync(host, Docs);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        provider.BeforePage = page => { if (page == 5) cancellation.Cancel(); };

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<InitialCrawler>().CrawlAsync(job.Id, cancellation.Token));
        Count(host, "Items").ShouldBe(400);
        await host.RestartAsync();
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        provider.BeforePage = null;
        var servedBefore = provider.ItemsServed;
        var summary = await CrawlAsync(host, job);

        summary.ShouldBe(new CrawlSummary(1, 0));
        Count(host, "Items").ShouldBe(1000);
        provider.ItemsServed.ShouldBe(servedBefore + 600);
        provider.ResumeTokensSeen[^6..].ShouldBe(["400", "500", "600", "700", "800", "900"]);
    }

    [Fact]
    public async Task store_a_delta_token_when_enumeration_completes()
    {
        var provider = new FakeCloudProvider(pageSize: 40);
        provider.GenerateTree(100, Docs);
        await using var host = CreateHost(provider, new RecordingProgressSink());
        var job = await SeedAsync(host, Docs);

        await CrawlAsync(host, job);
        var checkpoint = await host.Services.GetRequiredService<IItemIndexStore>().GetCheckpointAsync(new SyncScope(job.Id, Docs), Token);

        checkpoint.Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value.ShouldBe(new CrawlCheckpoint(null, 100, true, new DeltaToken("delta-docs")));
    }

    [Fact]
    public async Task continue_other_scopes_when_one_fails()
    {
        var provider = new FakeCloudProvider(pageSize: 50);
        provider.GenerateTree(120, Docs);
        provider.GenerateTree(30, Notes);
        provider.FailRoot(Docs, new Permanent("forbidden"));
        var sink = new RecordingProgressSink();
        await using var host = CreateHost(provider, sink);
        var job = await SeedAsync(host, Docs, Notes);

        var summary = await CrawlAsync(host, job);
        var store = host.Services.GetRequiredService<IItemIndexStore>();
        var docs = await store.GetCheckpointAsync(new SyncScope(job.Id, Docs), Token);
        var notes = await store.GetCheckpointAsync(new SyncScope(job.Id, Notes), Token);

        summary.ShouldBe(new CrawlSummary(1, 1));
        docs.Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value.ShouldBe(CrawlCheckpoint.None);
        notes.Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value.Completed.ShouldBeTrue();
        Count(host, "Items").ShouldBe(30);
        sink.Reports.ShouldContain(new CrawlProgress(job.Id, Docs, 0, false, Failed: true));
    }

    [Fact]
    public async Task retry_a_transient_failure_and_carry_on()
    {
        var provider = new FakeCloudProvider(pageSize: 50);
        provider.GenerateTree(120, Docs);
        provider.ScriptFailures(new Transient("network drop"), new RateLimited(TimeSpan.FromSeconds(30)));
        var sink = new RecordingProgressSink();
        await using var host = CreateHost(provider, sink);
        var job = await SeedAsync(host, Docs);

        var summary = await CrawlAsync(host, job);

        summary.ShouldBe(new CrawlSummary(1, 0));
        Count(host, "Items").ShouldBe(120);
        sink.Reports.Where(report => report.RetryIn is not null).Select(report => report.RetryIn).ShouldBe([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)]);
    }

    [Fact]
    public async Task skip_a_scope_that_already_completed()
    {
        var provider = new FakeCloudProvider(pageSize: 50);
        provider.GenerateTree(120, Docs);
        await using var host = CreateHost(provider, new RecordingProgressSink());
        var job = await SeedAsync(host, Docs);
        await CrawlAsync(host, job);
        var served = provider.PagesServed;

        var summary = await CrawlAsync(host, job);

        summary.ShouldBe(new CrawlSummary(1, 0));
        provider.PagesServed.ShouldBe(served);
    }

    [Fact]
    public async Task report_a_final_progress_event_per_scope()
    {
        var provider = new FakeCloudProvider(pageSize: 50);
        provider.GenerateTree(120, Docs);
        var sink = new RecordingProgressSink();
        await using var host = CreateHost(provider, sink);
        var job = await SeedAsync(host, Docs);

        await CrawlAsync(host, job);

        var last = sink.Reports.Last();
        (last.ItemsIndexed, last.Completed, last.ItemsTotal).ShouldBe((120, true, 120));
        sink.Reports.Count(report => report.Completed).ShouldBe(1);
    }

    [Fact]
    public async Task report_progress_with_a_total_and_an_estimate()
    {
        var provider = new FakeCloudProvider(pageSize: 50);
        provider.GenerateTree(120, Docs);
        var sink = new RecordingProgressSink();
        await using var host = CreateHost(provider, sink);
        var job = await SeedAsync(host, Docs);

        await CrawlAsync(host, job);

        var last = sink.Reports.Last();
        last.ItemsTotal.ShouldBe(120);
        last.ItemsPerSecond.ShouldNotBeNull().ShouldBeGreaterThan(0);
        last.EstimatedRemaining.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = CreateHost(new FakeCloudProvider(pageSize: 50), new RecordingProgressSink());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<InitialCrawler>().CrawlAsync(new SyncJobId(Guid.NewGuid()), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncRunner>().StartAsync(new SyncJobId(Guid.NewGuid()), cancelled.Token));
    }

    [Fact]
    public async Task index_700000_items_with_bounded_memory()
    {
        var provider = new FakeCloudProvider(pageSize: 5000);
        provider.GenerateTree(700_000, Docs);
        await using var host = CreateHost(provider, new RecordingProgressSink());
        var job = await SeedAsync(host, Docs);
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        var peak = baseline;
        provider.BeforePage = page => { if (page % 10 == 0) peak = Math.Max(peak, GC.GetTotalMemory(forceFullCollection: true)); };

        var summary = await CrawlAsync(host, job);

        summary.ShouldBe(new CrawlSummary(1, 0));
        Count(host, "Items").ShouldBe(700_000);
        (peak - baseline).ShouldBeLessThan(200L * 1024 * 1024);
    }

    private static DatabaseTestHost CreateHost(FakeCloudProvider provider, RecordingProgressSink sink)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<ICloudProvider>(provider);
            services.AddSingleton<ICrawlProgressSink>(sink);
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSyncEngine();
        });

    private static async Task<SyncJob> SeedAsync(DatabaseTestHost host, params RemoteItemId[] scopes)
    {
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", DateTimeOffset.UnixEpoch);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Download, true, account.CreatedUtc, account.CreatedUtc);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        foreach (var scope in scopes)
            (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, scope), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job;
    }

    private static async Task<CrawlSummary> CrawlAsync(DatabaseTestHost host, SyncJob job)
        => (await host.Services.GetRequiredService<InitialCrawler>().CrawlAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<CrawlSummary>>().Value;

    private static long Count(DatabaseTestHost host, string table)
    {
        using var connection = new SqliteConnection($"Data Source={host.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";

        return (long)command.ExecuteScalar()!;
    }
}
