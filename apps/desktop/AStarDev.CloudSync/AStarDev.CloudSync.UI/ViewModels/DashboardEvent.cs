namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>A notable event the dashboard lists under Recent Events.</summary>
internal enum DashboardEvent
{
    /// <summary>The operation queue ran empty.</summary>
    QueueDrained,

    /// <summary>A rename was detected on the provider.</summary>
    RemoteRenameDetected,

    /// <summary>A reconciliation pass finished.</summary>
    ReconciliationCompleted,

    /// <summary>The set of synchronised scopes changed.</summary>
    SyncScopeUpdated
}
