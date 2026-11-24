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
    public async Task observe_the_local_root_of_every_enabled_job()
    {
        await File.WriteAllTextAsync(Path.Combine(localRoot, "a.txt"), "abc", Token);
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, localRoot, Token);
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, new RemoteItemId("root")), Token);

        await host.Services.GetServices<IStartupStage>().OfType<BackgroundServicesStage>().Single().ExecuteAsync(Token);
        var store = host.Services.GetRequiredService<ILocalObservationStore>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        var present = new List<LocalSnapshot>();

        while (present.Count == 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, Token);
            present = [.. (await store.ListPresentAsync(job.Id, default, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<LocalSnapshot>>>().Value];
        }

        present.Select(snapshot => snapshot.Path.Value).ShouldBe(["a.txt"]);
    }

    [Fact]
    public async Task start_nothing_when_there_is_no_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetServices<IStartupStage>().OfType<BackgroundServicesStage>().Single().ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
    }

    [Fact]
    public async Task start_the_operation_scheduler()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, localRoot, Token);
        var operation = await host.AddOperationAsync(job, OperationStatus.Pending, Token);
        var store = host.Services.GetRequiredService<IOperationStore>();

        await host.Services.GetServices<IStartupStage>().OfType<BackgroundServicesStage>().Single().ExecuteAsync(Token);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        var status = OperationStatus.Pending;
        while (status == OperationStatus.Pending && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, Token);
            status = (await store.ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value.Single(row => row.Id == operation.Id).Status;
        }

        status.ShouldNotBe(OperationStatus.Pending);
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
