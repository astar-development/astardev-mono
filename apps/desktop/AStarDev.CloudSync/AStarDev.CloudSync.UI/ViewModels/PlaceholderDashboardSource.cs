namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The wireframe sample values of doc 17. The dashboard labels them as sample data until a real source replaces this one.</summary>
internal sealed class PlaceholderDashboardSource : IDashboardSource
{
    private static readonly DateTimeOffset Reconciled = new(2026, 1, 1, 14, 32, 11, TimeSpan.Zero);

    /// <inheritdoc/>
    public DashboardSnapshot GetSnapshot() => new(
        SyncHealth.Active,
        Reconciled,
        Reconciled.AddSeconds(47),
        Reconciled.AddSeconds(51),
        183442,
        702144,
        ReconciliationStage.EnumerateRemoteState,
        4232,
        TimeSpan.FromMinutes(17),
        [("OneDrive", SyncHealth.Healthy)],
        1245,
        8,
        181223,
        0,
        1,
        0,
        2,
        [
            (DashboardOperation.Download, "Documents/Architecture/Design.md"),
            (DashboardOperation.Upload, "Photos/2026/Holiday/Image123.jpg"),
            (DashboardOperation.Rename, "ProjectA -> ProjectArchive")
        ],
        [DashboardEvent.QueueDrained, DashboardEvent.RemoteRenameDetected, DashboardEvent.ReconciliationCompleted, DashboardEvent.SyncScopeUpdated]);
}
