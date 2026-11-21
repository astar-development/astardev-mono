namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>How much the user can rely on something, from best to worst. Each level has its own colour, icon and text, so meaning never rests on colour alone.</summary>
public enum TrustLevel
{
    /// <summary>Everything is as it should be.</summary>
    Healthy,

    /// <summary>Work is in progress and on track.</summary>
    Active,

    /// <summary>Working, but slower or less complete than normal.</summary>
    Degraded,

    /// <summary>The user should look at it soon.</summary>
    Attention,

    /// <summary>Data may be at risk or sync has stopped; the user must act.</summary>
    Critical
}
