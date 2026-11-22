namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Lets at most one progress report through per interval, so a fast crawl does not flood the UI.</summary>
/// <param name="interval">The least time between two reports that get through.</param>
/// <param name="timeProvider">The clock; a test passes its own.</param>
internal sealed class CrawlProgressThrottle(TimeSpan interval, TimeProvider timeProvider)
{
    private long? lastAdmitted;

    /// <summary>Asks whether a report may be sent now. The first ask is always admitted; an admitted ask restarts the interval.</summary>
    /// <returns><see langword="true"/> when the report may be sent; <see langword="false"/> when the last admitted report is younger than the interval.</returns>
    public bool TryAdmit()
    {
        var now = timeProvider.GetTimestamp();
        if (lastAdmitted is { } last && timeProvider.GetElapsedTime(last, now) < interval) return false;

        lastAdmitted = now;

        return true;
    }
}
