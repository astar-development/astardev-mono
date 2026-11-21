using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class DaemonServiceCollectionExtensionsShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task register_the_startup_stages_in_the_documented_order()
    {
        await using var host = DaemonTestHost.Create();

        host.Services.GetServices<IStartupStage>().Select(stage => stage.Name).ShouldBe(
        [
            "Process start",
            "Infrastructure initialisation",
            "State recovery",
            "Queue recovery",
            "Background services",
            "Accept connections"
        ]);
    }

    [Fact]
    public async Task start_the_ipc_server_only_after_the_startup_sequence()
    {
        await using var host = DaemonTestHost.Create();

        var hostedServices = host.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();

        hostedServices.IndexOf(typeof(StartupHostedService)).ShouldBeGreaterThanOrEqualTo(0);
        hostedServices.IndexOf(typeof(IpcHostedService)).ShouldBeGreaterThan(hostedServices.IndexOf(typeof(StartupHostedService)));
    }

    [Fact]
    public async Task answer_get_daemon_status_on_the_configured_socket_once_started()
    {
        await using var host = DaemonTestHost.Create();
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var status = (await client.QueryAsync(new GetDaemonStatus(), Token)).Value.ShouldBeOfType<Ok<DaemonStatusView>>().Value;
        await host.StopAsync(Token);

        status.State.ShouldBe(DaemonState.Running);
        status.Version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task stop_listening_when_the_host_stops()
    {
        await using var host = DaemonTestHost.Create();
        await using var connected = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await connected.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await host.StopAsync(Token);
        await connected.Disconnected.WaitAsync(Token);
        await using var late = UnixSocketIpc.CreateClient(host.SocketPath);

        (await late.ConnectAsync(Token)).Value.ShouldBeOfType<Fail<IpcError>>();
    }
}
