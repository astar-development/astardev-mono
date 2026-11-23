namespace AStarDev.CloudSync.Daemon;

/// <summary>Measures how fast operations complete, over the last 60 seconds of samples, for the dashboard's throughput and time left.</summary>
internal sealed class TransferRate(TimeProvider timeProvider)
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private readonly object gate = new();
    private readonly Queue<(DateTimeOffset At, int Completed)> samples = new();

    /// <summary>Records how many operations have completed now and reports the rate since the oldest sample inside the window.</summary>
    /// <param name="completed">Operations completed so far.</param>
    /// <returns>Completions per second, or <see langword="null"/> when nothing completed since the oldest sample or no time has passed, which is the first call. Safe to call from any thread.</returns>
    public double? Record(int completed)
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            samples.Enqueue((now, completed));
            while (samples.Count > 1 && now - samples.Peek().At > Window) samples.Dequeue();

            var oldest = samples.Peek();
            var seconds = (now - oldest.At).TotalSeconds;

            return seconds > 0 && completed > oldest.Completed ? (completed - oldest.Completed) / seconds : null;
        }
    }
}
