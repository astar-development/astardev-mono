using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal static class ConflictPages
{
    public static IReadOnlyDictionary<Guid, ConflictsView> Of(params ConflictView[] conflicts)
        => conflicts.GroupBy(conflict => conflict.SyncJobId).ToDictionary(job => job.Key, job => new ConflictsView(job.Count(), [.. job]));
}
