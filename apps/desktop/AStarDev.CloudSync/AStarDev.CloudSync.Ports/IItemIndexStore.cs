using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where the items found by the initial crawl and the crawl's progress are kept between runs. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IItemIndexStore
{
    /// <summary>Reads how far the crawl of a scope has got.</summary>
    /// <param name="scope">The scope to look up.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The checkpoint, <see cref="CrawlCheckpoint.None"/> when the scope was never crawled, or <see cref="StorageFailed"/>.</returns>
    Task<Result<CrawlCheckpoint, StoreError>> GetCheckpointAsync(SyncScope scope, CancellationToken cancellationToken);

    /// <summary>Stores one page of items and the checkpoint that goes with it, all or nothing.</summary>
    /// <param name="scope">The scope the items were listed under.</param>
    /// <param name="items">The page. An item already stored for the job, found by its <see cref="ProviderItem.RemoteItemId"/>, is updated instead of added twice, so storing a page again is safe.</param>
    /// <param name="checkpoint">The state to keep once the items are stored.</param>
    /// <param name="observedUtc">When the provider reported the items.</param>
    /// <param name="cancellationToken">Cancelling before the write completes stores neither the items nor the checkpoint.</param>
    /// <returns>Success, <see cref="SyncScopeNotFound"/> when the scope is not selected, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> SavePageAsync(SyncScope scope, IReadOnlyList<ProviderItem> items, CrawlCheckpoint checkpoint, DateTimeOffset observedUtc, CancellationToken cancellationToken);
}
