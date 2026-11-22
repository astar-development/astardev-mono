namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The <c>LocalObservation</c> configuration section: how local changes are batched before they are looked at.</summary>
public sealed class LocalObservationOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string ConfigurationSection = "LocalObservation";

    /// <summary>How long the disk must stay quiet after a change before the changed paths are scanned. A new change restarts the wait, so a burst becomes one scan. Zero or less scans at once.</summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromSeconds(2);
}
