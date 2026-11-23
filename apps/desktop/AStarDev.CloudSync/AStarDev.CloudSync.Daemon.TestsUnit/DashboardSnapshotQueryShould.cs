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

    private static async Task<(DaemonTestHost Host, SyncJob Job)> HostWithJobAsync(RecordingIpcServer? server = null, TimeProvider? clock = null)
    {
        var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(new StubCloudProvider()).RemoveAll<IIpcServer>().AddSingleton<IIpcServer>(server ?? new RecordingIpcServer()).RemoveAll<TimeProvider>().AddSingleton(clock ?? TimeProvider.System));
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
    public async Task report_no_account_when_none_is_connected()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var dashboard = await SnapshotAsync(host);

        dashboard.Setup.ShouldBe(DashboardSetup.NoAccount);
        dashboard.Stage.ShouldBeNull();
    }

    [Fact]
    public async Task report_no_folders_when_the_account_has_no_selected_folder()
    {
        var (host, _) = await HostWithJobAsync();
        await using var _ = host;

        var dashboard = await SnapshotAsync(host);

        dashboard.Setup.ShouldBe(DashboardSetup.NoFolders);
        dashboard.Stage.ShouldBeNull();
    }

    [Fact]
    public async Task report_ready_with_no_stage_before_any_sync_has_run()
    {
        var (host, job) = await HostWithJobAsync();
        await using var _ = host;
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, new RemoteItemId("docs")), Token);

        var dashboard = await SnapshotAsync(host);

        dashboard.Setup.ShouldBe(DashboardSetup.Ready);
        dashboard.Stage.ShouldBeNull();
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
    public async Task report_the_crawl_progress_with_the_activity_estimate()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var (host, job) = await HostWithJobAsync(clock: clock);
        await using var _ = host;
        var sink = host.Services.GetRequiredService<ICrawlProgressSink>();
        await sink.ReportAsync(new CrawlProgress(job.Id, new RemoteItemId("docs"), 0, false, 100), Token);
        clock.Advance(TimeSpan.FromSeconds(5));
        await sink.ReportAsync(new CrawlProgress(job.Id, new RemoteItemId("docs"), 40, false, 100), Token);

        var dashboard = await SnapshotAsync(host);

        dashboard.ItemsAnalysed.ShouldBe(40);
        dashboard.ItemsTotal.ShouldBe(100);
        dashboard.ItemsPerSecond.ShouldBe(8);
        dashboard.EstimatedRemaining.ShouldBe(TimeSpan.FromSeconds(8));
        host.Services.GetRequiredService<ActivityTracker>().Current().Activities.Single().EtaSeconds.ShouldBe(8);
        dashboard.Stage.ShouldBe(DashboardStage.EnumerateRemoteState);
    }

    [Fact]
    public async Task report_transfer_progress_as_completed_over_all_operations_once_indexing_has_ended()
    {
        var (host, job) = await HostWithJobAsync();
        await using var _ = host;
        var finished = await host.AddOperationAsync(job, OperationStatus.Running, Token);
        await host.Services.GetRequiredService<IOperationStore>().CompleteAsync(finished.Id, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);

        var dashboard = await SnapshotAsync(host);

        dashboard.ItemsAnalysed.ShouldBe(1);
        dashboard.ItemsTotal.ShouldBe(3);
    }

    [Fact]
    public async Task report_the_transfer_rate_and_time_left_from_recent_completions()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var (host, job) = await HostWithJobAsync(clock: clock);
        await using var _ = host;
        var finished = await host.AddOperationAsync(job, OperationStatus.Running, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        await SnapshotAsync(host);
        await host.Services.GetRequiredService<IOperationStore>().CompleteAsync(finished.Id, Token);
        clock.Advance(TimeSpan.FromSeconds(10));

        var dashboard = await SnapshotAsync(host);

        dashboard.ItemsPerSecond.ShouldNotBeNull().ShouldBe(0.1, 0.001);
        dashboard.EstimatedRemaining.ShouldBe(TimeSpan.FromSeconds(20));
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
