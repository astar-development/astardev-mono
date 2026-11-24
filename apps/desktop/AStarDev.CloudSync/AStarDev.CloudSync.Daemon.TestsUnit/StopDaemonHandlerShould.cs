using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class StopDaemonHandlerShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<bool> StoppedAsync(DaemonTestHost host)
    {
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (!lifetime.ApplicationStopping.IsCancellationRequested && DateTimeOffset.UtcNow < deadline) await Task.Delay(20, Token);

        return lifetime.ApplicationStopping.IsCancellationRequested;
    }

    [Fact]
    public async Task answer_then_stop_the_host_with_exit_code_zero()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        var result = await host.Services.GetRequiredService<StopDaemonHandler>().HandleAsync(new StopDaemon(false), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        (await StoppedAsync(host)).ShouldBeTrue();
        host.Services.GetRequiredService<DaemonExit>().Code.ShouldBe(0);
    }

    [Fact]
    public async Task stop_with_the_restart_exit_code_so_systemd_starts_the_daemon_again()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        await host.Services.GetRequiredService<StopDaemonHandler>().HandleAsync(new StopDaemon(true), Token);

        (await StoppedAsync(host)).ShouldBeTrue();
        host.Services.GetRequiredService<DaemonExit>().Code.ShouldBe(ApplicationConstants.RestartExitCode);
    }

    [Fact]
    public async Task serve_the_command_over_ipc()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new StopDaemon(false), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        (await StoppedAsync(host)).ShouldBeTrue();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<StopDaemonHandler>().HandleAsync(new StopDaemon(true), cancelled.Token));
    }
}
