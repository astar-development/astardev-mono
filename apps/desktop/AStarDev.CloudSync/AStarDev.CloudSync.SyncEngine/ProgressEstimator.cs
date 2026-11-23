using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Measures how fast items are being processed over a sliding window, so a stall or a burst shows up soon instead of being averaged over the whole run.</summary>
/// <param name="window">How far back samples count; older ones are dropped.</param>
/// <param name="timeProvider">The clock; a test passes its own.</param>
internal sealed class ProgressEstimator(TimeSpan window, TimeProvider timeProvider)
{
    private readonly Queue<(long Timestamp, long Items)> samples = [];

    /// <summary>Notes how many items are done now. Call after each page; samples older than the window are discarded.</summary>
    /// <param name="itemsDone">Items done so far, never less than the previous call.</param>
    public void Record(long itemsDone)
    {
        var now = timeProvider.GetTimestamp();
        samples.Enqueue((now, itemsDone));
        while (samples.Count > 2 && timeProvider.GetElapsedTime(samples.Peek().Timestamp, now) > window) samples.Dequeue();
    }

    /// <summary>Items per second over the window.</summary>
    /// <returns>None until two samples that differ in time exist.</returns>
    public Option<double> ItemsPerSecond()
    {
        if (samples.Count < 2) return Option.None<double>();

        var (firstTimestamp, firstItems) = samples.Peek();
        var (lastTimestamp, lastItems) = samples.Last();
        var seconds = timeProvider.GetElapsedTime(firstTimestamp, lastTimestamp).TotalSeconds;

        return seconds > 0 ? Option.Some((lastItems - firstItems) / seconds) : Option.None<double>();
    }

    /// <summary>Time left at the current throughput.</summary>
    /// <param name="itemsDone">Items done so far.</param>
    /// <param name="total">Items in all; None when the provider did not say.</param>
    /// <returns>None when the total is unknown or throughput is zero, so no guess is shown.</returns>
    public Option<TimeSpan> Remaining(long itemsDone, Option<long> total)
        => total.Bind(all => ItemsPerSecond().Bind(rate => rate > 0 ? Option.Some(TimeSpan.FromSeconds(Math.Max(0, all - itemsDone) / rate)) : Option.None<TimeSpan>()));
}
