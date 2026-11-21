namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The overall health the Trust Status card reports, from best to worst.</summary>
internal enum SyncHealth
{
    /// <summary>All selected scopes are synchronised and nothing needs doing.</summary>
    Healthy,

    /// <summary>Sync work is running and on track.</summary>
    Active,

    /// <summary>Sync works but is slower or less complete than normal.</summary>
    Degraded,

    /// <summary>The user has something to resolve, such as a conflict.</summary>
    AttentionRequired,

    /// <summary>Sync has stopped or data may be at risk.</summary>
    Critical
}
