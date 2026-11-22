using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class BackgroundServicesStageShould : IDisposable
{
    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-stage-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task resume_the_crawl_of_every_stored_job()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([StubCloudProvider.Item("a")], null, new DeltaToken("delta-1"))) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, localRoot, Token);
        var scope = new SyncScope(job.Id, new RemoteItemId("docs"));
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, Token);

        var result = await host.Services.GetServices<IStartupStage>().OfType<BackgroundServicesStage>().Single().ExecuteAsync(Token);
        var checkpoint = await CompletedAsync(host, scope);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        checkpoint.Completed.ShouldBeTrue();
    }

    [Fact]
    public async Task start_nothing_when_there_is_no_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetServices<IStartupStage>().OfType<BackgroundServicesStage>().Single().ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task<CrawlCheckpoint> CompletedAsync(DaemonTestHost host, SyncScope scope)
    {
        var store = host.Services.GetRequiredService<IItemIndexStore>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        var checkpoint = CrawlCheckpoint.None;

        while (!checkpoint.Completed && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, Token);
            checkpoint = (await store.GetCheckpointAsync(scope, Token)).Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value;
        }

        return checkpoint;
    }
}
