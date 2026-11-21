namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Where the dashboard gets its numbers. A later slice implements it over IPC and registers it; the view and view model do not change.</summary>
internal interface IDashboardSource
{
    /// <summary>The current dashboard values. Always returns a complete snapshot; zero counts and empty lists mean there is nothing to report.</summary>
    public DashboardSnapshot GetSnapshot();
}
