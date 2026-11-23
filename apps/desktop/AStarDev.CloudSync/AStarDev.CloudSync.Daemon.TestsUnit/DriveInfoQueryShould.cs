using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class DriveInfoQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task answer_with_the_provider_drive_info()
    {
        var provider = new StubCloudProvider { DriveInfoResult = new Ok<ProviderDriveInfo>(new ProviderDriveInfo(30, 70, 100, Option.None<long>())) };
        await using var host = DaemonTestHost.CreateWith(provider);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetDriveInfo(), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<DriveInfoView>>().Value.ShouldBe(new DriveInfoView(30, 70, 100, null));
    }

    [Fact]
    public async Task carry_a_known_item_count_estimate()
    {
        var provider = new StubCloudProvider { DriveInfoResult = new Ok<ProviderDriveInfo>(new ProviderDriveInfo(1, 2, 3, Option.Some(42L))) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var query = host.Services.GetRequiredService<DriveInfoQuery>();

        var result = await query.HandleAsync(new GetDriveInfo(), Token);

        result.Value.ShouldBeOfType<Ok<DriveInfoView>>().Value.ItemCountEstimate.ShouldBe(42);
    }

    [Fact]
    public async Task return_the_provider_error_as_a_failure()
    {
        var provider = new StubCloudProvider { DriveInfoResult = new Fail<ProviderError>(new AuthenticationFailed("Sign in first.")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetDriveInfo(), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Fail<IpcError>>().Error.Value.ShouldBeOfType<HandlerFailed>().Message.ShouldBe("Sign in first.");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var query = host.Services.GetRequiredService<DriveInfoQuery>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => query.HandleAsync(new GetDriveInfo(), cancelled.Token));
    }
}
