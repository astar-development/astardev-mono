using System.Collections.Concurrent;
using System.Diagnostics;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class RecordedSpans : IDisposable
{
    private readonly ConcurrentBag<Activity> stopped = [];
    private readonly ActivityListener listener;

    public RecordedSpans()
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("AStarDev.CloudSync.", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);
    }

    public IReadOnlyList<Activity> All => [.. stopped];

    public IReadOnlyList<Activity> ForJob(SyncJobId syncJobId)
        => [.. stopped.Where(activity => activity.GetTagItem("cloudsync.sync_job.id") is string id && id == syncJobId.Value.ToString())];

    public void Dispose() => listener.Dispose();
}
