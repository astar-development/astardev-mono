namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetActivities"/>.</summary>
/// <param name="Activities">One entry per job the daemon has reported on since it started; empty when nothing has run.</param>
public sealed record ActivitiesView(IReadOnlyList<ActivityView> Activities);
