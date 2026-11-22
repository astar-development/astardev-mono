using System.Net;
using System.Text;
using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.OneDrive;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class TelemetryRun : IAsyncDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");
    private static readonly RemoteItemId Downloaded = new("a");
    private static readonly RemoteItemId Failing = new("b");
    private static readonly RemoteItemId Conflicting = new("c");
    private static readonly RemoteItemId Flaky = new("d");

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cloudsync-telemetry-{Guid.NewGuid():N}");
    private readonly FakeCloudProvider provider = new(pageSize: 100);
    private readonly IHost host;

    public TelemetryRun()
    {
        Spans = new RecordedSpans();
        Metrics = new RecordedMetrics();
        Logs = new CapturingLoggerProvider();
        LocalRoot = Path.Combine(directory, "local");
        SocketPath = Path.Combine(directory, "daemon.sock");
        Directory.CreateDirectory(LocalRoot);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{Daemon.ApplicationConstants.IpcConfigurationSection}:{nameof(IpcOptions.SocketPath)}"] = SocketPath;
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = Path.Combine(directory, "cloudsync.db");
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(Logs);
        builder.Services.AddCloudSyncDaemon();
        builder.Services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(provider);
        host = builder.Build();
    }

    public RecordedSpans Spans { get; }

    public RecordedMetrics Metrics { get; }

    public CapturingLoggerProvider Logs { get; }

    public string LocalRoot { get; }

    public string SocketPath { get; }

    public IServiceProvider Services => host.Services;

    public SyncJobId Job { get; private set; }

    public ItemId DownloadedItem { get; private set; }

    public ItemId ConflictingItem { get; private set; }

    public async Task DriveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(cancellationToken);
        await SeedAsync(cancellationToken);
        await CrawlAsync(cancellationToken);
        await PlanAndRunAsync(cancellationToken);
        await ResolveConflictAsync(cancellationToken);
        await ApplyDeltaAsync(cancellationToken);
        await RequestFromGraphAsync(cancellationToken);
        await StartAsync(cancellationToken);
        await QueryStatusAsync(cancellationToken);
        Metrics.Observe();
        await StopAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken) => host.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => host.StopAsync(cancellationToken);

    public async Task<DaemonStatusView> QueryStatusAsync(CancellationToken cancellationToken)
    {
        await using var client = UnixSocketIpc.CreateClient(SocketPath);
        (await client.ConnectAsync(cancellationToken)).Value.ShouldBeOfType<Ok<Unit>>();

        return (await client.QueryAsync(new GetDaemonStatus(), cancellationToken)).Value.ShouldBeOfType<Ok<DaemonStatusView>>().Value;
    }

    public async ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else host.Dispose();

        Metrics.Dispose();
        Spans.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", LocalRoot, Root, BootstrapMode.Merge, false, Stamp, Stamp);
        Job = job.Id;
        (await Services.GetRequiredService<IAccountStore>().AddAsync(account, cancellationToken)).Value.ShouldBeOfType<Ok<Unit>>();
        (await Services.GetRequiredService<ISyncJobStore>().AddAsync(job, cancellationToken)).Value.ShouldBeOfType<Ok<Unit>>();
        (await Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), cancellationToken)).Value.ShouldBeOfType<Ok<Unit>>();
        AddRemote(Downloaded, "a.txt", "remote-a");
        AddRemote(Failing, "b.txt", null);
        AddRemote(Conflicting, "c.txt", "remote-c");
        AddRemote(Flaky, "d.txt", null);
        provider.TransientDownloads.Add(Flaky);
    }

    private void AddRemote(RemoteItemId id, string path, string? content)
    {
        provider.AddItem(new ProviderItem(id, new RelativePath(path), ItemType.File, 8, Stamp.AddHours(2)));
        if (content is not null) provider.Contents[id] = Encoding.UTF8.GetBytes(content);
    }

    private async Task CrawlAsync(CancellationToken cancellationToken)
    {
        var runner = Services.GetRequiredService<SyncRunner>();
        (await runner.StartAsync(Job, cancellationToken)).Value.ShouldBeOfType<Ok<Unit>>();
        await WaitForAsync(() => Spans.ForJob(Job).Any(span => span.OperationName == "cloudsync.delta.process"), cancellationToken);
        await runner.StopAsync(Job, cancellationToken);
    }

    private async Task PlanAndRunAsync(CancellationToken cancellationToken)
    {
        var full = Path.Combine(LocalRoot, "c.txt");
        await File.WriteAllTextAsync(full, "local-c", cancellationToken);
        File.SetLastWriteTimeUtc(full, Stamp.UtcDateTime.AddHours(1));
        var local = new LocalSnapshot(new RelativePath("c.txt"), 7, Stamp.AddHours(1), null, new ContentFingerprint("fp-local-c"));
        (await Services.GetRequiredService<ILocalObservationStore>().RecordAsync(Job, [local], [], Stamp.AddHours(3), cancellationToken)).Value.ShouldBeOfType<Ok<Unit>>();
        var items = (await Services.GetRequiredService<IItemStateStore>().ListAsync(Job, cancellationToken)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;
        DownloadedItem = items.Single(item => item.Path.Value == "a.txt").ItemId;
        var conflicting = items.Single(item => item.Path.Value == "c.txt");
        ConflictingItem = conflicting.ItemId;
        var known = Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        (await known.StageAsync(Job, operation, conflicting.ItemId, new KnownSnapshot(conflicting.Path, 5, Stamp.AddDays(-1), null, null), cancellationToken)).Value.ShouldBeOfType<Ok<Unit>>();
        (await known.CommitAsync(operation, cancellationToken)).Value.ShouldBeOfType<Ok<Unit>>();

        (await Services.GetRequiredService<SyncPlanner>().PlanAsync(Job, cancellationToken)).Value.ShouldBeOfType<Ok<PlanSummary>>();
        await RunQueueAsync(cancellationToken);
    }

    private async Task ResolveConflictAsync(CancellationToken cancellationToken)
    {
        var open = (await Services.GetRequiredService<IConflictStore>().ListOpenAsync(Job, cancellationToken)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value;
        var results = await Services.GetRequiredService<ConflictResolver>().ResolveAsync([open.Single().Id], ConflictResolution.KeepLocal, cancellationToken);
        results.Single().Value.ShouldBeOfType<Ok<Unit>>();
        await RunQueueAsync(cancellationToken);
    }

    private async Task ApplyDeltaAsync(CancellationToken cancellationToken)
    {
        var checkpoint = (await Services.GetRequiredService<IItemIndexStore>().GetCheckpointAsync(new SyncScope(Job, Root), cancellationToken)).Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value;
        provider.ExpireToken(checkpoint.DeltaToken!.Value);
        provider.ScriptChanges(Root, new ProviderChange(ChangeKind.Deleted, Downloaded));

        (await Services.GetRequiredService<DeltaProcessingService>().ProcessScopeAsync(new SyncScope(Job, Root), cancellationToken)).Value.ShouldBeOfType<Ok<int>>();
    }

    private async Task RequestFromGraphAsync(CancellationToken cancellationToken)
    {
        var handler = new QueuedGraphHandler()
            .Then(HttpStatusCode.OK, """{ "value": [], "@odata.deltaLink": "https://graph.test/delta?token=next" }""")
            .Then(HttpStatusCode.TooManyRequests);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };
        var enumerator = new RootEnumerator(client, Services.GetRequiredService<OneDriveTelemetry>());

        (await enumerator.GetDeltasAsync(Root, new DeltaToken("https://graph.test/delta?token=old"), null, cancellationToken)).Value.ShouldBeOfType<Ok<ProviderDeltaPage>>();
        (await enumerator.GetDeltasAsync(Root, new DeltaToken("https://graph.test/delta?token=next"), null, cancellationToken)).Value.ShouldBeOfType<Fail<ProviderError>>();
    }

    private async Task RunQueueAsync(CancellationToken cancellationToken)
    {
        var scheduler = Services.GetRequiredService<OperationScheduler>();
        while ((await scheduler.RunOnceAsync(cancellationToken)).Value is Ok<bool> { Value: true }) { }
    }

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!condition()) await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
    }
}
