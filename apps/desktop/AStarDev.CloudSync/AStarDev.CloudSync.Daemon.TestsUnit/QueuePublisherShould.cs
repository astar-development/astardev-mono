using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class QueuePublisherShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static QueuePublisher PublisherOf(DaemonTestHost host) => host.Services.GetServices<IHostedService>().OfType<QueuePublisher>().Single();

    [Fact]
    public async Task publish_the_queue_the_status_and_the_dashboard_when_operations_move_without_a_crawl()
    {
        var server = new RecordingIpcServer();
        await using var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(new StubCloudProvider()).RemoveAll<IIpcServer>().AddSingleton<IIpcServer>(server));
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-publisher", Token);
        var publisher = PublisherOf(host);

        await publisher.PublishAsync(Token);
        await host.AddOperationAsync(job, OperationStatus.Running, Token);
        await publisher.PublishAsync(Token);

        server.Published.OfType<QueueChanged>().Select(changed => changed.Queue.Running).ShouldBe([0, 1]);
        server.Published.OfType<DashboardChanged>().Select(changed => changed.Dashboard.Running).ShouldBe([0, 1]);
        server.Published.OfType<SyncStatusChanged>().Select(changed => changed.Status.QueueDepth).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => PublisherOf(host).PublishAsync(cancelled.Token));
    }

    [Fact]
    public async Task start_after_the_ipc_server_so_it_never_publishes_before_clients_can_listen()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        var hostedServices = host.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();

        hostedServices.IndexOf(typeof(QueuePublisher)).ShouldBeGreaterThan(hostedServices.IndexOf(typeof(IpcHostedService)));
    }
}
