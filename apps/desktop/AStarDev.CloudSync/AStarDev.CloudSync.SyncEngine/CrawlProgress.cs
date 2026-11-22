using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>How far the initial crawl of one scope has got, as reported to <see cref="ICrawlProgressSink"/>.</summary>
/// <param name="SyncJobId">The job being crawled.</param>
/// <param name="RemoteRootId">The scope, that is the remote folder, being crawled.</param>
/// <param name="ItemsIndexed">Items stored so far for the scope, including those stored before a restart.</param>
/// <param name="Completed"><see langword="true"/> for the last report of a scope, sent even when the throttle would otherwise hold it back.</param>
/// <param name="ItemsTotal">Items the scope holds in all, when the provider said; <see langword="null"/> when unknown, including in reports sent while waiting to retry.</param>
/// <param name="RetryIn">How long the crawl waits before trying the provider again; <see langword="null"/> when it is not waiting. Such a report is sent at once, not throttled.</param>
/// <param name="Failed"><see langword="true"/> when the scope stopped on an error and needs another start. <paramref name="ItemsIndexed"/> is then not meaningful.</param>
public sealed record CrawlProgress(SyncJobId SyncJobId, RemoteItemId RemoteRootId, long ItemsIndexed, bool Completed, long? ItemsTotal = null, TimeSpan? RetryIn = null, bool Failed = false);
