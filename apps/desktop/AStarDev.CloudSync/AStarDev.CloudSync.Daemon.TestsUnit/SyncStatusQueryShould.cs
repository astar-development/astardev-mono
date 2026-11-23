using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class SyncStatusQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<SyncStatusView> StatusAsync(DaemonTestHost host)
    {
        var result = await host.Services.GetRequiredService<SyncStatusQuery>().HandleAsync(new GetSyncStatus(), Token);

        return result.Value.ShouldBeOfType<Ok<SyncStatusView>>().Value;
    }

    private static async Task<(DaemonTestHost Host, SyncJob Job)> HostWithJobAsync(RecordingIpcServer? server = null)
    {
        var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(new StubCloudProvider()).RemoveAll<IIpcServer>().AddSingleton<IIpcServer>(server ?? new RecordingIpcServer()));
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);

        return (host, await host.AddJobAsync(account, "/tmp/cloudsync-status", Token));
    }

    [Fact]
    public async Task report_queue_depth_from_the_operation_store()
    {
        var (host, job) = await HostWithJobAsync();
        await using var _ = host;
        await host.AddOperationAsync(job, OperationStatus.DeadLetter, Token);
        await host.AddOperationAsync(job, OperationStatus.Running, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);

        var status = await StatusAsync(host);

        status.QueueDepth.ShouldBe(2);
        status.ErrorCount.ShouldBe(1);
    }

    [Fact]
    public async Task report_provider_health_from_the_health_checks()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        (await StatusAsync(host)).ProviderHealth.ShouldBe(ProviderHealthState.Healthy);

        await host.AddAccountAsync("old", AccountStatus.ReauthenticationRequired, Token);

        (await StatusAsync(host)).ProviderHealth.ShouldBe(ProviderHealthState.Degraded);
    }

    [Fact]
    public async Task report_the_last_activity_of_the_crawl()
    {
        var (host, job) = await HostWithJobAsync();
        await using var _ = host;
        (await StatusAsync(host)).LastActivityAt.ShouldBeNull();

        await host.Services.GetRequiredService<ICrawlProgressSink>().ReportAsync(new CrawlProgress(job.Id, new RemoteItemId("docs"), 40, false, 100), Token);

        (await StatusAsync(host)).LastActivityAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task publish_a_status_change_when_queue_activity_changes()
    {
        var server = new RecordingIpcServer();
        var (host, job) = await HostWithJobAsync(server);
        await using var _ = host;
        var query = host.Services.GetRequiredService<SyncStatusQuery>();

        await query.PublishIfChangedAsync(Token);
        await query.PublishIfChangedAsync(Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        await query.PublishIfChangedAsync(Token);

        var published = server.Published.OfType<SyncStatusChanged>().Select(changed => changed.Status.QueueDepth).ToList();
        published.ShouldBe([0, 1]);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncStatusQuery>().HandleAsync(new GetSyncStatus(), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncStatusQuery>().PublishIfChangedAsync(cancelled.Token));
    }
}
