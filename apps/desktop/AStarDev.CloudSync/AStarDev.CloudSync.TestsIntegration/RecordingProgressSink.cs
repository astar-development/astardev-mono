using AStarDev.CloudSync.SyncEngine;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class RecordingProgressSink : ICrawlProgressSink
{
    public List<CrawlProgress> Reports { get; } = [];

    public Task ReportAsync(CrawlProgress progress, CancellationToken cancellationToken)
    {
        lock (Reports) Reports.Add(progress);

        return Task.CompletedTask;
    }
}
