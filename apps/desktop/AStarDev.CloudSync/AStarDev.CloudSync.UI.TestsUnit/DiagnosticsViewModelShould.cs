using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class DiagnosticsViewModelShould
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (Microsoft.Extensions.Hosting.IHost Host, DiagnosticsViewModel Screen) Create(FakeIpcClient client)
    {
        var host = OperationScreensHost.Create(client, new FixedTimeProvider(Now));

        return (host, host.Screen<DiagnosticsViewModel>());
    }

    [Fact]
    public async Task show_the_diagnostics_from_the_daemon()
    {
        var client = new FakeIpcClient([], available: true)
        {
            DiagnosticsReply = new Ok<DiagnosticsView>(new DiagnosticsView(DaemonState.Running, "1.2.3", Now - new TimeSpan(2, 3, 4, 5), [new HealthCheckView("provider", ProviderHealthState.Degraded), new HealthCheckView("queue", ProviderHealthState.Healthy)]))
        };
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.RefreshAsync(Token);

        screen.Version.ShouldBe("1.2.3");
        screen.State.ShouldBe("Running");
        screen.Uptime.ShouldBe("2 d 3 h 4 min");
        screen.Checks.ShouldBe([new HealthCheckRowViewModel("provider", "Degraded"), new HealthCheckRowViewModel("queue", "Healthy")]);
        screen.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task keep_the_diagnostics_and_say_why_when_the_daemon_is_unreachable()
    {
        var reachable = new FakeIpcClient([], available: true);
        var (host, screen) = Create(reachable);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        reachable.DiagnosticsReply = new Fail<IpcError>(new HandlerFailed("The diagnostics could not be read."));

        await screen.RefreshAsync(Token);

        screen.Version.ShouldBe("1.0.0");
        screen.ErrorMessage.ShouldBe("The diagnostics could not be read.");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var (host, screen) = Create(new FakeIpcClient([], available: true));
        using var lifetime = host;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => screen.RefreshAsync(cancelled.Token));
    }
}
