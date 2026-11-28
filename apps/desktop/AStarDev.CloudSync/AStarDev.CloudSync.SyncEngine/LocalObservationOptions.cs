namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The <c>LocalObservation</c> configuration section: how local changes are batched before they are looked at.</summary>
public sealed class LocalObservationOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string ConfigurationSection = "LocalObservation";

    /// <summary>How long the disk must stay quiet after a change before the changed paths are scanned. A new change restarts the wait, so a burst becomes one scan. Zero or less scans at once.</summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>How many changed files a scan hashes at once. The default of two keeps a spinning disk from seeking between files; less than one is treated as one and more than four as four. Files are recorded in disk order whatever this is.</summary>
    public int FingerprintParallelism { get; set; } = 2;
}
