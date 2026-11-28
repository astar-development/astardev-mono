using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class QueuePublisherShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static QueuePublisher PublisherOf(DaemonTestHost host) => host.Services.GetServices<IHostedService>().OfType<QueuePublisher>().Single();

    private static DaemonTestHost CountingHost(CommandCounter commands, RecordingIpcServer server, TimeProvider clock)
        => DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(new StubCloudProvider()).RemoveAll<IIpcServer>().AddSingleton<IIpcServer>(server).RemoveAll<TimeProvider>().AddSingleton(clock).ConfigureDbContext<CloudSyncDbContext>(options => options.AddInterceptors(commands)));

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
    public async Task read_nothing_from_the_database_while_nothing_is_written()
    {
        var commands = new CommandCounter();
        await using var host = CountingHost(commands, new RecordingIpcServer(), TimeProvider.System);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-publisher", Token);
        await host.AddOperationAsync(job, OperationStatus.Running, Token);
        var publisher = PublisherOf(host);
        await publisher.PublishAsync(Token);
        commands.Reset();

        await publisher.PublishAsync(Token);
        await publisher.PublishAsync(Token);
        await publisher.PublishAsync(Token);

        commands.Commands.ShouldBe(0);
    }

    [Fact]
    public async Task publish_the_first_tick_after_a_write_that_follows_idle_ticks()
    {
        var server = new RecordingIpcServer();
        await using var host = CountingHost(new CommandCounter(), server, TimeProvider.System);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-publisher", Token);
        var publisher = PublisherOf(host);
        await publisher.PublishAsync(Token);
        await publisher.PublishAsync(Token);

        await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        await publisher.PublishAsync(Token);

        server.Published.OfType<QueueChanged>().Select(changed => changed.Queue.Pending).ShouldBe([0, 1]);
        server.Published.OfType<DashboardChanged>().Select(changed => changed.Dashboard.Pending).ShouldBe([0, 1]);
        server.Published.OfType<SyncStatusChanged>().Select(changed => changed.Status.QueueDepth).ShouldBe([0, 1]);
    }

    [Fact]
    public async Task keep_publishing_the_falling_transfer_rate_while_nothing_is_written()
    {
        var commands = new CommandCounter();
        var server = new RecordingIpcServer();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        await using var host = CountingHost(commands, server, clock);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-publisher", Token);
        var finished = await host.AddOperationAsync(job, OperationStatus.Running, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        var publisher = PublisherOf(host);
        await publisher.PublishAsync(Token);
        await host.Services.GetRequiredService<IOperationStore>().CompleteAsync(finished.Id, Token);
        clock.Advance(TimeSpan.FromSeconds(10));
        await publisher.PublishAsync(Token);
        commands.Reset();

        clock.Advance(TimeSpan.FromSeconds(10));
        await publisher.PublishAsync(Token);

        commands.Commands.ShouldBe(0);
        server.Published.OfType<DashboardChanged>().Last().Dashboard.ItemsPerSecond.ShouldNotBeNull().ShouldBe(0.05, 0.001);
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
