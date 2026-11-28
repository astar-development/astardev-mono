using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class ItemIndexStoreShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task save_a_page_with_more_items_than_sqlite_allows_parameters_twice()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var scope = new SyncScope(job.Id, new RemoteItemId("root"));
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, Token);
        var store = host.Services.GetRequiredService<IItemIndexStore>();
        var items = Enumerable.Range(0, 40_000).Select(index => new ProviderItem(new RemoteItemId($"remote-{index}"), new RelativePath($"f{index}.txt"), ItemType.File, index, Stamp)).ToList();

        var first = await store.SavePageAsync(scope, items, new CrawlCheckpoint(null, items.Count, true, null), Stamp, Token);
        var second = await store.SavePageAsync(scope, items, new CrawlCheckpoint(null, items.Count, true, null), Stamp.AddSeconds(1), Token);

        first.Value.ShouldBeOfType<Ok<Unit>>();
        second.Value.ShouldBeOfType<Ok<Unit>>();
    }
}
