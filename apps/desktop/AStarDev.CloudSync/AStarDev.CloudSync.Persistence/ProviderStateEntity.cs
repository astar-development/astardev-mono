using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored provider bookmark for one sync job. There is at most one row per job.</summary>
internal sealed class ProviderStateEntity
{
    /// <summary>Primary key and foreign key to the job; deleting the job deletes its state.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary><see langword="null"/> until the first full crawl completes, and again when the provider invalidates the token.</summary>
    public DeltaToken? DeltaToken { get; set; }

    /// <summary>When a sync last completed successfully; <see langword="null"/> if none has.</summary>
    public DateTimeOffset? LastSyncUtc { get; set; }

    /// <summary>When the provider or disk was last scanned; <see langword="null"/> if never.</summary>
    public DateTimeOffset? LastScanUtc { get; set; }
}
