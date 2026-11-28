using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AStarDev.CloudSync.TestsIntegration.DaemonTests;

public sealed class DeltaPollingShould : IDisposable
{
    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-polling-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task poll_after_the_crawl_completes()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([StubCloudProvider.Item("a")], null, new DeltaToken("delta-1"))) };
        var configuration = new Dictionary<string, string?> { [$"{DeltaPollingOptions.ConfigurationSection}:{nameof(DeltaPollingOptions.Interval)}"] = "00:00:00.050" };
        await using var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(provider), configuration: configuration);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, localRoot, Token);
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, new RemoteItemId("docs")), Token);

        await host.Services.GetRequiredService<SyncRunner>().StartAsync(job.Id, Token);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (provider.DeltaCalls < 3 && DateTimeOffset.UtcNow < deadline) await Task.Delay(20, Token);

        provider.DeltaCalls.ShouldBeGreaterThanOrEqualTo(3);
        provider.Roots.Count.ShouldBe(1);
        await host.Services.GetRequiredService<SyncRunner>().StopAsync(job.Id, Token);
    }
}
