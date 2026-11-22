namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The <c>OperationScheduler</c> configuration section: how many operations run at once and how failures are retried.</summary>
public sealed class OperationSchedulerOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string ConfigurationSection = "OperationScheduler";

    /// <summary>How many workers claim operations at once. Operations for one item never overlap whatever this is. Less than one is treated as one.</summary>
    public int WorkerCount { get; set; } = 4;

    /// <summary>How many attempts an operation gets, the first included, before it is dead-lettered. Less than one is treated as one.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The wait after the first failed attempt; each further failure doubles it.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How long an idle worker waits before it looks for due operations again.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
}
