namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The kind of operation the dashboard shows as current activity.</summary>
internal enum DashboardOperation
{
    /// <summary>A file is being fetched from the provider.</summary>
    Download,

    /// <summary>A file is being sent to the provider.</summary>
    Upload,

    /// <summary>A file or folder is being renamed.</summary>
    Rename
}
