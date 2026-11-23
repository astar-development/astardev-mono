namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Published while the initial crawl runs, at most a few times per second, and once more when a folder is finished. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.
/// The daemon counts; the UI only shows the numbers.
/// </summary>
/// <param name="SyncJobId">The job being crawled.</param>
/// <param name="RemoteRootId">The selected folder being crawled, as in <see cref="SyncScopeView.RemoteRootId"/>.</param>
/// <param name="ItemsIndexed">Items stored so far for the folder, including those stored before a restart.</param>
/// <param name="Phase">Whether the folder is being listed for the first time or having changes applied.</param>
/// <param name="ItemsTotal">Items the folder holds in all; <see langword="null"/> when the provider did not say.</param>
/// <param name="Completed"><see langword="true"/> on the last report for the folder, after which no more items are expected from it.</param>
public sealed record CrawlProgressChanged(Guid SyncJobId, string RemoteRootId, long ItemsIndexed, bool Completed, CrawlPhase Phase = CrawlPhase.Crawling, long? ItemsTotal = null) : IEvent;
