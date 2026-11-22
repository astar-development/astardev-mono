namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The <c>DeltaPolling</c> configuration section: how often a job whose scopes have all been crawled asks the provider for changes.</summary>
public sealed class DeltaPollingOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string ConfigurationSection = "DeltaPolling";

    /// <summary>The wait between one round of delta processing and the next. Zero or less is treated as one second.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
}
