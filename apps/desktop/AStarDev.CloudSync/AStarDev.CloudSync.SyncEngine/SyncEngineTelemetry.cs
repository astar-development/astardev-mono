using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The spans and metrics of the initial crawl (doc 12). Collected by name, so the engine needs no reference to Observability.</summary>
internal static class SyncEngineTelemetry
{
    private static readonly ActivitySource Source = new(ApplicationConstants.TelemetryName);
    private static readonly Meter Meter = new(ApplicationConstants.TelemetryName);
    private static readonly Counter<long> ItemsIndexed = Meter.CreateCounter<long>("cloudsync.syncengine.items_indexed", "{item}", "Items stored by the initial crawl.");
    private static readonly Histogram<double> CrawlDuration = Meter.CreateHistogram<double>("cloudsync.syncengine.crawl.duration", "s", "How long the crawl of one scope took, from start to finish or failure.");

    /// <summary>Starts the span of one scope's crawl. <see langword="null"/> when nothing listens; dispose it to end the span.</summary>
    /// <param name="remoteRootId">The scope's folder id, recorded on the span.</param>
    public static Activity? StartScope(string remoteRootId)
    {
        var activity = Source.StartActivity("crawl.scope");
        activity?.SetTag("cloudsync.scope", remoteRootId);

        return activity;
    }

    /// <summary>Starts the span of one page fetched and stored. <see langword="null"/> when nothing listens; dispose it to end the span.</summary>
    public static Activity? StartPage() => Source.StartActivity("crawl.page");

    /// <summary>Counts items stored.</summary>
    /// <param name="count">How many items the page held.</param>
    public static void RecordIndexed(int count) => ItemsIndexed.Add(count);

    /// <summary>Records how long one scope's crawl took.</summary>
    /// <param name="elapsed">Time from the first page to the end of the crawl of the scope.</param>
    public static void RecordDuration(TimeSpan elapsed) => CrawlDuration.Record(elapsed.TotalSeconds);
}
