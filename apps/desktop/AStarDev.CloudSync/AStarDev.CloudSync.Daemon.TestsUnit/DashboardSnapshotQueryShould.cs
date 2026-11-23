using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class DashboardSnapshotQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<DashboardView> SnapshotAsync(DaemonTestHost host)
    {
        var result = await host.Services.GetRequiredService<DashboardSnapshotQuery>().HandleAsync(new GetDashboardSnapshot(), Token);

        return result.Value.ShouldBeOfType<Ok<DashboardView>>().Value;
    }

    private static async Task<(DaemonTestHost Host, SyncJob Job)> HostWithJobAsync(RecordingIpcServer? server = null)
    {
        var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(new StubCloudProvider()).RemoveAll<IIpcServer>().AddSingleton<IIpcServer>(server ?? new RecordingIpcServer()));
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);

        return (host, await host.AddJobAsync(account, "/tmp/cloudsync-dashboard", Token));
    }

    [Fact]
    public async Task report_counts_from_the_stores()
    {
        var (host, job) = await HostWithJobAsync();
        await using var _ = host;
        await host.AddOperationAsync(job, OperationStatus.DeadLetter, Token);
        await host.AddOperationAsync(job, OperationStatus.Running, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);

        var dashboard = await SnapshotAsync(host);

        dashboard.Pending.ShouldBe(2);
        dashboard.Running.ShouldBe(1);
        dashboard.Errors.ShouldBe(1);
        dashboard.Conflicts.ShouldBe(0);
        dashboard.Providers.ShouldBe([new DashboardProviderView("OneDrive", ProviderHealthState.Healthy)]);
        dashboard.Stage.ShouldBe(DashboardStage.ExecuteQueue);
    }

    [Fact]
    public async Task report_the_running_operations()
    {
        var (host, job) = await HostWithJobAsync();
        await using var _ = host;
        await host.AddOperationAsync(job, OperationStatus.Running, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);

        var dashboard = await SnapshotAsync(host);

        dashboard.Activities.Select(activity => activity.Type).ShouldBe(["UploadFile"]);
    }

    [Fact]
    public async Task report_the_crawl_progress_with_its_own_estimate()
    {
        var (host, job) = await HostWithJobAsync();
        await using var _ = host;
        await host.Services.GetRequiredService<ICrawlProgressSink>().ReportAsync(new CrawlProgress(job.Id, new RemoteItemId("docs"), 40, false, 100, ItemsPerSecond: 8, EstimatedRemaining: TimeSpan.FromSeconds(7)), Token);

        var dashboard = await SnapshotAsync(host);

        dashboard.ItemsAnalysed.ShouldBe(40);
        dashboard.ItemsTotal.ShouldBe(100);
        dashboard.ItemsPerSecond.ShouldBe(8);
        dashboard.EstimatedRemaining.ShouldBe(TimeSpan.FromSeconds(7));
        dashboard.Stage.ShouldBe(DashboardStage.EnumerateRemoteState);
    }

    [Fact]
    public async Task publish_a_change_when_the_queue_moves()
    {
        var server = new RecordingIpcServer();
        var (host, job) = await HostWithJobAsync(server);
        await using var _ = host;
        var query = host.Services.GetRequiredService<DashboardSnapshotQuery>();

        await query.PublishIfChangedAsync(Token);
        await query.PublishIfChangedAsync(Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        await query.PublishIfChangedAsync(Token);

        server.Published.OfType<DashboardChanged>().Select(changed => changed.Dashboard.Pending).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var query = host.Services.GetRequiredService<DashboardSnapshotQuery>();

        await Should.ThrowAsync<OperationCanceledException>(() => query.HandleAsync(new GetDashboardSnapshot(), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => query.PublishIfChangedAsync(cancelled.Token));
    }
}
