using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class IpcHostedServiceShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task start_only_after_the_recovery_stages()
    {
        var ran = new List<string>();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IStartupStage>(new FakeStage("state recovery", ran));
        builder.Services.AddSingleton<IStartupStage>(new FakeStage("queue recovery", ran));
        builder.Services.AddSingleton<StartupSequence>();
        builder.Services.AddSingleton<IIpcServer>(new FakeIpcServer(ran));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHostedService<StartupHostedService>();
        builder.Services.AddHostedService<IpcHostedService>();
        using var host = builder.Build();

        await host.StartAsync(Token);

        ran.ShouldBe(["state recovery", "queue recovery", "ipc"]);
    }

    [Fact]
    public async Task answer_get_daemon_status()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-daemon-{Guid.NewGuid():N}.sock");
        await using var server = UnixSocketIpc.CreateServer(socketPath);
        await using var client = UnixSocketIpc.CreateClient(socketPath);
        var service = new IpcHostedService(server, TimeProvider.System);

        await service.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var status = (await client.QueryAsync(new GetDaemonStatus(), Token)).Value.ShouldBeOfType<Ok<DaemonStatusView>>().Value;
        await service.StopAsync(Token);

        status.State.ShouldBe(DaemonState.Running);
        status.Version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task stop_the_server_when_stopped()
    {
        var server = new FakeIpcServer([]);
        var service = new IpcHostedService(server, TimeProvider.System);

        await service.StopAsync(Token);

        server.Stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task honour_cancellation_when_starting()
    {
        var service = new IpcHostedService(new FakeIpcServer([]), TimeProvider.System);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => service.StartAsync(source.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_stopping()
    {
        var service = new IpcHostedService(new FakeIpcServer([]), TimeProvider.System);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => service.StopAsync(source.Token));
    }
}
