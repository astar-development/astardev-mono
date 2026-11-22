using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Turns crawl progress into <see cref="CrawlProgressChanged"/> events for every subscribed UI.</summary>
internal sealed class IpcCrawlProgressSink(IIpcServer server, ILogger<IpcCrawlProgressSink> logger) : ICrawlProgressSink
{
    /// <inheritdoc/>
    /// <remarks>A failed publish is logged and dropped: a UI that misses one report sees the next.</remarks>
    public async Task ReportAsync(CrawlProgress progress, CancellationToken cancellationToken)
    {
        var @event = new CrawlProgressChanged(progress.SyncJobId.Value, progress.RemoteRootId.Value, progress.ItemsIndexed, progress.Completed);
        if (await server.PublishAsync(@event, cancellationToken) is Fail<IpcError> fail) logger.LogWarning("Publishing crawl progress failed: {Error}", fail.Error.Value);
    }
}
