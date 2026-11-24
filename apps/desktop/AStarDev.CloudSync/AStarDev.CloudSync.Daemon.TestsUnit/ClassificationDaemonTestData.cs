using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal static class ClassificationDaemonTestData
{
    internal static async Task<DaemonTestHost> SeededHostAsync(CancellationToken cancellationToken, Action<IServiceCollection>? configureServices = null)
    {
        var host = DaemonTestHost.Create(configureServices: configureServices);
        await host.InitialiseDatabaseAsync(cancellationToken);
        await host.Services.GetRequiredService<IClassificationStartup>().RunAsync(cancellationToken);

        return host;
    }

    internal static async Task SaveFilesAsync(DaemonTestHost host, CancellationToken cancellationToken, params string[] paths)
    {
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, cancellationToken);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-classification", cancellationToken);
        var scope = new SyncScope(job.Id, new RemoteItemId("root"));
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, cancellationToken);
        var items = paths.Select((path, index) => new ProviderItem(new RemoteItemId($"remote-{index}"), new RelativePath(path), ItemType.File, 10, DateTimeOffset.UnixEpoch));
        await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [.. items], CrawlCheckpoint.None, DateTimeOffset.UnixEpoch, cancellationToken);
    }
}
