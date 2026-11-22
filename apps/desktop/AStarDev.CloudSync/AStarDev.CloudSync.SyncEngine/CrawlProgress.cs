using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>How far the initial crawl of one scope has got, as reported to <see cref="ICrawlProgressSink"/>.</summary>
/// <param name="SyncJobId">The job being crawled.</param>
/// <param name="RemoteRootId">The scope, that is the remote folder, being crawled.</param>
/// <param name="ItemsIndexed">Items stored so far for the scope, including those stored before a restart.</param>
/// <param name="Completed"><see langword="true"/> for the last report of a scope, sent even when the throttle would otherwise hold it back.</param>
public sealed record CrawlProgress(SyncJobId SyncJobId, RemoteItemId RemoteRootId, long ItemsIndexed, bool Completed);
