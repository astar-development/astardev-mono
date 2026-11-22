using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetActivities"/> from the <see cref="ActivityTracker"/>.</summary>
internal sealed class ActivitiesQuery(ActivityTracker tracker)
{
    /// <summary>Reports what every job is doing now.</summary>
    /// <returns>The current activities, empty when nothing has run since the daemon started. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<ActivitiesView, IpcError>> HandleAsync(GetActivities query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<ActivitiesView, IpcError>>(new Ok<ActivitiesView>(tracker.Current()));
    }
}
