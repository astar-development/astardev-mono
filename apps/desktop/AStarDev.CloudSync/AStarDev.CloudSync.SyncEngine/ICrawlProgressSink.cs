namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Where the sync engine sends crawl progress. The daemon implements it by publishing an IPC event; the engine never knows about IPC.</summary>
public interface ICrawlProgressSink
{
    /// <summary>Receives one progress report. At most one arrives per throttle interval for a running crawl, plus one final report per scope.</summary>
    /// <param name="progress">The state to show.</param>
    /// <param name="cancellationToken">Cancelling abandons the delivery and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Completes when the report is delivered. A sink that cannot deliver drops the report instead of failing the crawl; progress is not worth stopping for.</returns>
    Task ReportAsync(CrawlProgress progress, CancellationToken cancellationToken);
}
