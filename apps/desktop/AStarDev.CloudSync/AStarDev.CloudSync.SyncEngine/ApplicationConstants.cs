namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Project-specific values for the sync engine, so none are repeated as literals.</summary>
internal static class ApplicationConstants
{
    public const string TelemetryName = "AStarDev.CloudSync.SyncEngine";

    public const int MaxPageAttempts = 5;

    public static TimeSpan RetryBaseDelay { get; } = TimeSpan.FromSeconds(2);

    public static TimeSpan ProgressInterval { get; } = TimeSpan.FromMilliseconds(500);
}
