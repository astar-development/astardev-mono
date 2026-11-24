using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification.TestsUnit;

internal static class SyncTestData
{
    internal static readonly DateTimeOffset Now = new(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);

    internal static ProviderItem File(string remoteId, string path) => new(new RemoteItemId(remoteId), new RelativePath(path), ItemType.File, 10, Now);

    internal static async Task<SyncScope> AddScopeAsync(ClassificationTestHost host, CancellationToken cancellationToken)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Now);
        await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, cancellationToken);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Merge, true, Now, Now);
        await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, cancellationToken);
        var scope = new SyncScope(job.Id, new RemoteItemId("root"));
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, cancellationToken);

        return scope;
    }

    internal static async Task SaveFilesAsync(ClassificationTestHost host, CancellationToken cancellationToken, params string[] paths)
    {
        var scope = await AddScopeAsync(host, cancellationToken);
        await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [.. paths.Select((path, index) => File($"remote-{index}", path))], CrawlCheckpoint.None, Now, cancellationToken);
    }
}
