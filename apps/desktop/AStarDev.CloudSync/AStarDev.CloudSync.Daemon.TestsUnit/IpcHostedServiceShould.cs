using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class IpcHostedServiceShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
