using System.Text;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class LocalObservationShould : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("cloudsync-observe-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public async Task scan_at_startup()
    {
        Write("a.txt", "abc");
        Write("sub/b.txt", "The quick brown fox jumps over the lazy dog");
        await using var host = CreateHost();
        var job = await SeedAsync(host);

        var result = await host.Services.GetRequiredService<LocalObserver>().ScanAsync(job, default, Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        Query(host, "SELECT i.LocalPath || '|' || i.RemoteItemId || '|' || s.Size || '|' || s.Hash FROM Items i JOIN ItemSnapshots s ON s.ItemId = i.Id WHERE s.SnapshotSource = 'Local' ORDER BY i.LocalPath")
            .ShouldBe(["a.txt||3|YRDDGAAAAAAAAAAAAwAAAAAAAAA=", "sub/b.txt||43|bMSlbysmxJL6S75XwfMcQZOpcr4="]);
    }

    [Fact]
    public async Task not_record_a_second_snapshot_for_an_unchanged_file()
    {
        Write("a.txt", "abc");
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        var observer = host.Services.GetRequiredService<LocalObserver>();

        await observer.ScanAsync(job, default, Token);
        await observer.ScanAsync(job, default, Token);

        Query(host, "SELECT COUNT(*) FROM ItemSnapshots").ShouldBe(["1"]);
    }

    [Fact]
    public async Task record_a_created_modified_and_deleted_file()
    {
        Write("seed.txt", "seed");
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var run = host.Services.GetRequiredService<LocalObserver>().RunAsync(job, cancellation.Token);
        await WaitAsync(host, "SELECT COUNT(*) FROM ItemSnapshots", "1");

        Write("new.txt", "abc");
        await WaitAsync(host, "SELECT s.Size || '|' || s.Hash FROM ItemSnapshots s JOIN Items i ON i.Id = s.ItemId WHERE i.LocalPath = 'new.txt'", "3|YRDDGAAAAAAAAAAAAwAAAAAAAAA=");

        Write("new.txt", "abcd");
        await WaitAsync(host, "SELECT s.Size FROM ItemSnapshots s JOIN Items i ON i.Id = s.ItemId WHERE i.LocalPath = 'new.txt' ORDER BY s.ObservedUtc DESC LIMIT 1", "4");

        File.Delete(Path.Combine(root, "new.txt"));
        await WaitAsync(host, "SELECT s.Size FROM ItemSnapshots s JOIN Items i ON i.Id = s.ItemId WHERE i.LocalPath = 'new.txt' ORDER BY s.ObservedUtc DESC LIMIT 1", "-1");

        Query(host, "SELECT COUNT(*) FROM Items WHERE LocalPath = 'new.txt'").ShouldBe(["1"]);
        Query(host, "SELECT COUNT(*) FROM ItemSnapshots s JOIN Items i ON i.Id = s.ItemId WHERE i.LocalPath = 'new.txt' AND s.SnapshotSource = 'Local'").ShouldBe(["3"]);
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () => await run);
    }

    [Fact]
    public async Task rescan_after_watcher_overflow()
    {
        Write("seed.txt", "seed");
        var fileSystem = new OverflowingFileSystem(new PhysicalLocalFileSystem());
        await using var host = CreateHost(fileSystem);
        var job = await SeedAsync(host);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var run = host.Services.GetRequiredService<LocalObserver>().RunAsync(job, cancellation.Token);
        await WaitAsync(host, "SELECT COUNT(*) FROM ItemSnapshots", "1");

        Write("missed.txt", "abc");
        File.Delete(Path.Combine(root, "seed.txt"));
        fileSystem.Overflow();

        await WaitAsync(host, "SELECT COUNT(*) FROM Items WHERE LocalPath = 'missed.txt'", "1");
        await WaitAsync(host, "SELECT s.Size FROM ItemSnapshots s JOIN Items i ON i.Id = s.ItemId WHERE i.LocalPath = 'seed.txt' ORDER BY s.ObservedUtc DESC LIMIT 1", "-1");
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () => await run);
    }

    private DatabaseTestHost CreateHost(ILocalFileSystem? fileSystem = null)
        => DatabaseTestHost.Create(services =>
        {
            if (fileSystem is not null) services.AddSingleton(fileSystem);
            services.AddSingleton<ICloudProvider>(new FakeCloudProvider(pageSize: 100));
            services.AddSingleton<ICrawlProgressSink>(new RecordingProgressSink());
            services.AddSingleton(TimeProvider.System);
            services.AddSyncEngine();
            services.Configure<LocalObservationOptions>(options => options.Debounce = TimeSpan.FromMilliseconds(50));
        });

    private async Task<SyncJob> SeedAsync(DatabaseTestHost host)
    {
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", DateTimeOffset.UnixEpoch);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", root, new RemoteItemId("root"), BootstrapMode.Merge, true, account.CreatedUtc, account.CreatedUtc);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job;
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Encoding.ASCII);
    }

    private static async Task WaitAsync(DatabaseTestHost host, string sql, string expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        var rows = Query(host, sql);
        while (!rows.SequenceEqual([expected]) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, Token);
            rows = Query(host, sql);
        }

        rows.ShouldBe([expected]);
    }

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
