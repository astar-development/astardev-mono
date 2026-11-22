using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.FakeProvider;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class DeltaProcessingShould
{
    private static readonly RemoteItemId Docs = new("docs");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task apply_created_modified_deleted_and_renamed_items()
    {
        var provider = new FakeCloudProvider(pageSize: 100);
        AddFiles(provider, "a", "b", "c", "d");
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await CrawlAsync(host, job);
        provider.ScriptChanges(
            Docs,
            new ProviderChange(ChangeKind.Created, new RemoteItemId("e"), File("e", 5)),
            new ProviderChange(ChangeKind.Modified, new RemoteItemId("a"), File("a", 99)),
            new ProviderChange(ChangeKind.Deleted, new RemoteItemId("b")),
            new ProviderChange(ChangeKind.Renamed, new RemoteItemId("c"), new ProviderItem(new RemoteItemId("c"), new RelativePath("moved/c.txt"), ItemType.File, 1, DateTimeOffset.UnixEpoch), new RelativePath("c.txt")));

        var outcome = await host.Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(new SyncScope(job.Id, Docs), Token);

        outcome.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(4);
        Query(host, "SELECT RemoteItemId || '=' || LocalPath FROM Items ORDER BY RemoteItemId").ShouldBe(["a=a.txt", "c=moved/c.txt", "d=d.txt", "e=e.txt"]);
        Query(host, "SELECT s.Size FROM ItemSnapshots s JOIN Items i ON i.Id = s.ItemId WHERE i.RemoteItemId = 'a' AND s.SnapshotSource = 'Remote' ORDER BY s.Size").ShouldBe(["1", "99"]);
        Query(host, "SELECT COUNT(*) FROM ItemSnapshots").ShouldBe(["5"]);
        (await TokenAsync(host, job)).ShouldBe(new DeltaToken("delta-docs-1"));
    }

    [Fact]
    public async Task store_the_new_token_only_after_the_page_is_applied()
    {
        var provider = new FakeCloudProvider(pageSize: 100);
        AddFiles(provider, "a");
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await CrawlAsync(host, job);
        provider.ScriptChanges(
            Docs,
            new ProviderChange(ChangeKind.Created, new RemoteItemId("e"), File("e", 5)),
            new ProviderChange(ChangeKind.Created, new RemoteItemId("f"), new ProviderItem(new RemoteItemId("f"), default, ItemType.File, 1, DateTimeOffset.UnixEpoch)));

        var outcome = await host.Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(new SyncScope(job.Id, Docs), Token);

        outcome.Value.ShouldBeOfType<Fail<DeltaError>>();
        (await TokenAsync(host, job)).ShouldBe(new DeltaToken("delta-docs"));
        Query(host, "SELECT RemoteItemId FROM Items").ShouldBe(["a"]);
    }

    [Fact]
    public async Task apply_every_page_and_keep_the_token_of_the_last()
    {
        var provider = new FakeCloudProvider(pageSize: 2);
        AddFiles(provider, "a");
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await CrawlAsync(host, job);
        provider.ScriptChanges(Docs, new[] { "b", "c", "d", "e", "f" }.Select(id => new ProviderChange(ChangeKind.Created, new RemoteItemId(id), File(id, 1))).ToArray());

        var outcome = await host.Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(new SyncScope(job.Id, Docs), Token);

        outcome.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(5);
        Query(host, "SELECT COUNT(*) FROM Items").ShouldBe(["6"]);
        (await TokenAsync(host, job)).ShouldBe(new DeltaToken("delta-docs-1"));
    }

    [Fact]
    public async Task refuse_a_scope_that_was_not_crawled()
    {
        await using var host = CreateHost(new FakeCloudProvider(pageSize: 100));
        var job = await SeedAsync(host);

        var outcome = await host.Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(new SyncScope(job.Id, Docs), Token);

        outcome.Value.ShouldBeOfType<Fail<DeltaError>>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = CreateHost(new FakeCloudProvider(pageSize: 100));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var scope = new SyncScope(new SyncJobId(Guid.NewGuid()), Docs);

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(scope, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<DeltaProcessingService>().ProcessJobAsync(scope.SyncJobId, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IItemIndexStore>().ApplyDeltaPageAsync(scope, [], null, DateTimeOffset.UnixEpoch, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IItemIndexStore>().ResetScopeAsync(scope, cancelled.Token));
    }

    [Fact]
    public async Task reindex_the_scope_when_the_token_is_invalid()
    {
        var provider = new FakeCloudProvider(pageSize: 100);
        AddFiles(provider, "a", "b");
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await CrawlAsync(host, job);
        provider.AddItem(File("c", 3), Docs);
        provider.ExpireToken(new DeltaToken("delta-docs"));

        var outcome = await host.Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(new SyncScope(job.Id, Docs), Token);

        outcome.Value.ShouldBeOfType<Ok<int>>();
        Query(host, "SELECT RemoteItemId FROM Items ORDER BY RemoteItemId").ShouldBe(["a", "b", "c"]);
        (await TokenAsync(host, job)).ShouldBe(new DeltaToken("delta-docs-1"));
        provider.ResumeTokensSeen.Count.ShouldBe(2);
    }

    [Fact]
    public async Task leave_other_scopes_alone_during_a_reindex()
    {
        var pictures = new RemoteItemId("pictures");
        var provider = new FakeCloudProvider(pageSize: 100);
        AddFiles(provider, "a");
        provider.AddItem(File("p", 1), pictures);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, pictures), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        await CrawlAsync(host, job);
        var before = (await host.Services.GetRequiredService<IItemIndexStore>().GetCheckpointAsync(new SyncScope(job.Id, pictures), Token)).Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value;
        var pagesBefore = provider.PagesServed;
        provider.ExpireToken(new DeltaToken("delta-docs"));

        var outcome = await host.Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(new SyncScope(job.Id, Docs), Token);

        outcome.Value.ShouldBeOfType<Ok<int>>();
        provider.PagesServed.ShouldBe(pagesBefore + 1);
        var after = (await host.Services.GetRequiredService<IItemIndexStore>().GetCheckpointAsync(new SyncScope(job.Id, pictures), Token)).Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value;
        after.ShouldBe(before);
        Query(host, "SELECT COUNT(*) FROM ItemSnapshots s JOIN Items i ON i.Id = s.ItemId WHERE i.RemoteItemId = 'p'").ShouldBe(["1"]);
        Query(host, "SELECT COUNT(*) FROM Items").ShouldBe(["2"]);
    }

    private static ProviderItem File(string id, long size) => new(new RemoteItemId(id), new RelativePath($"{id}.txt"), ItemType.File, size, DateTimeOffset.UnixEpoch);

    private static void AddFiles(FakeCloudProvider provider, params string[] ids)
    {
        foreach (var id in ids) provider.AddItem(File(id, 1), Docs);
    }

    private static DatabaseTestHost CreateHost(FakeCloudProvider provider)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<ICloudProvider>(provider);
            services.AddSingleton<ICrawlProgressSink>(new RecordingProgressSink());
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSyncEngine();
        });

    private static async Task<SyncJob> SeedAsync(DatabaseTestHost host)
    {
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", DateTimeOffset.UnixEpoch);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Download, true, account.CreatedUtc, account.CreatedUtc);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Docs), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job;
    }

    private static async Task CrawlAsync(DatabaseTestHost host, SyncJob job)
        => (await host.Services.GetRequiredService<InitialCrawler>().CrawlAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<CrawlSummary>>();

    private static async Task<DeltaToken?> TokenAsync(DatabaseTestHost host, SyncJob job)
        => (await host.Services.GetRequiredService<IItemIndexStore>().GetCheckpointAsync(new SyncScope(job.Id, Docs), Token)).Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value.DeltaToken;

    private static List<string> Query(DatabaseTestHost host, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={host.DatabasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read()) rows.Add(Convert.ToString(reader.GetValue(0))!);

        return rows;
    }
}
