using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Records crawl progress in the <see cref="ActivityTracker"/> and tells every subscribed UI with <see cref="CrawlProgressChanged"/> and <see cref="ActivityChanged"/> events.</summary>
internal sealed class IpcCrawlProgressSink(IIpcServer server, ActivityTracker tracker, SyncStatusQuery status, DashboardSnapshotQuery dashboard, ILogger<IpcCrawlProgressSink> logger) : ICrawlProgressSink
{
    /// <inheritdoc/>
    /// <remarks>A failed publish is logged and dropped: a UI that misses one report sees the next.</remarks>
    public async Task ReportAsync(CrawlProgress progress, CancellationToken cancellationToken)
    {
        var activity = await tracker.ApplyAsync(progress, cancellationToken);
        await PublishAsync(new CrawlProgressChanged(progress.SyncJobId.Value, progress.RemoteRootId.Value, progress.ItemsIndexed, progress.Completed, progress.Delta ? CrawlPhase.Syncing : CrawlPhase.Crawling, progress.ItemsTotal), cancellationToken);
        await PublishAsync(new ActivityChanged(activity), cancellationToken);
        if (await status.PublishIfChangedAsync(cancellationToken) is Fail<IpcError> fail) DaemonLog.PublishFailed(logger, nameof(SyncStatusChanged), fail.Error.Value, ErrorInfo.CodeOf(fail.Error.Value));
        if (await dashboard.PublishIfChangedAsync(cancellationToken) is Fail<IpcError> dashboardFail) DaemonLog.PublishFailed(logger, nameof(DashboardChanged), dashboardFail.Error.Value, ErrorInfo.CodeOf(dashboardFail.Error.Value));
    }

    private async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent
    {
        if (await server.PublishAsync(@event, cancellationToken) is Fail<IpcError> fail) DaemonLog.PublishFailed(logger, typeof(TEvent).Name, fail.Error.Value, ErrorInfo.CodeOf(fail.Error.Value));
    }
}
