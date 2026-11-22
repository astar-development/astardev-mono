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

    /// <summary>Forgets how far the crawl of a scope got and its delta token, so the next crawl lists the scope again from the start. Stored items and snapshots stay.</summary>
    /// <param name="scope">The scope to reset; no other scope is touched.</param>
    /// <param name="cancellationToken">Cancelling before the write completes resets nothing.</param>
    /// <returns>Success, also when the scope had no checkpoint, so calling twice is safe; <see cref="SyncScopeNotFound"/> when the scope is not selected; or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> ResetScopeAsync(SyncScope scope, CancellationToken cancellationToken);

    /// <summary>Applies one page of remote changes to the stored items and snapshots, and keeps the scope's new delta token, all or nothing.</summary>
    /// <param name="scope">The scope the changes were reported for.</param>
    /// <param name="changes">The page, applied in order. Created and modified items are added or updated by <see cref="ProviderChange.RemoteItemId"/> with a new remote snapshot; renamed items get their new path; deleted items are removed together with their snapshots and any items below them. A change for an unknown item is applied as far as it can be, so applying a page again is safe.</param>
    /// <param name="newToken">The token to keep once the changes are stored; <see langword="null"/> for a page that is not the last, which leaves the stored token as it is.</param>
    /// <param name="observedUtc">When the provider reported the changes.</param>
    /// <param name="cancellationToken">Cancelling before the write completes stores neither the changes nor the token.</param>
    /// <returns>Success, <see cref="SyncScopeNotFound"/> when the scope is not selected, or <see cref="StorageFailed"/>, in which case the old token is still stored.</returns>
    Task<Result<Unit, StoreError>> ApplyDeltaPageAsync(SyncScope scope, IReadOnlyList<ProviderChange> changes, DeltaToken? newToken, DateTimeOffset observedUtc, CancellationToken cancellationToken);
}
