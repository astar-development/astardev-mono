using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ProvidersQueryShould
{
    private static readonly DateTimeOffset Synced = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ProvidersView> AskAsync(DaemonTestHost host)
    {
        await host.InitialiseDatabaseAsync(Token);
        var result = await host.Services.GetRequiredService<ProvidersQuery>().HandleAsync(new GetProviders(), Token);

        return result.Value.ShouldBeOfType<Ok<ProvidersView>>().Value;
    }

    private static async Task RecordSyncAsync(DaemonTestHost host, SyncJob job)
    {
        var scope = new SyncScope(job.Id, job.RemoteRootId);
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [], new CrawlCheckpoint(null, 0, true, new DeltaToken("delta")), Synced, Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    [Fact]
    public async Task report_the_capabilities_of_the_registered_provider()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider { Features = [ProviderFeature.Versioning, ProviderFeature.RecycleBin] });

        var view = await AskAsync(host);

        var provider = view.Providers.ShouldHaveSingleItem();
        provider.ProviderType.ShouldBe("OneDrive");
        provider.Capabilities.ShouldBe([ProviderCapability.Versioning, ProviderCapability.RecycleBin]);
    }

    [Fact]
    public async Task report_every_feature_of_the_port_as_a_capability()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider { Features = Enum.GetValues<ProviderFeature>() });

        var view = await AskAsync(host);

        view.Providers.ShouldHaveSingleItem().Capabilities.Select(capability => capability.ToString()).ShouldBe(Enum.GetNames<ProviderFeature>());
    }

    [Fact]
    public async Task report_no_capabilities_when_the_provider_offers_none()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider { Features = [] });

        var view = await AskAsync(host);

        view.Providers.ShouldHaveSingleItem().Capabilities.ShouldBeEmpty();
    }

    [Fact]
    public async Task report_last_sync_per_account()
    {
        var provider = new StubCloudProvider { DriveInfoResult = new Ok<ProviderDriveInfo>(new ProviderDriveInfo(40, 60, 100, Option.None<long>())) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var first = await host.AddAccountAsync("first", AccountStatus.Connected, Token);
        var second = await host.AddAccountAsync("second", AccountStatus.ReauthenticationRequired, Token);
        var job = await host.AddJobAsync(first, "/tmp/cloudsync-providers", Token);
        await RecordSyncAsync(host, job);
        await host.AddOperationAsync(job, OperationStatus.DeadLetter, Token);
        await host.AddOperationAsync(job, OperationStatus.Pending, Token);

        var view = await AskAsync(host);

        view.Accounts.ShouldBe(
        [
            new ProviderInsightsView(first.Id.Value, Synced, 40, 100, 1, 0, 1),
            new ProviderInsightsView(second.Id.Value, null, null, null, 0, 0, 0)
        ]);
    }

    [Fact]
    public async Task leave_storage_empty_when_the_provider_cannot_say()
    {
        var provider = new StubCloudProvider { DriveInfoResult = new Fail<ProviderError>(new Transient("offline")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("first", AccountStatus.Connected, Token);

        var view = await AskAsync(host);

        view.Accounts.ShouldBe([new ProviderInsightsView(account.Id.Value, null, null, null, 0, 0, 0)]);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ProvidersQuery>().HandleAsync(new GetProviders(), cancelled.Token));
    }
}
