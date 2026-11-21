using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of a configured sync relationship.</summary>
internal sealed class SyncJobEntity
{
    /// <summary>Primary key.</summary>
    public SyncJobId Id { get; set; }

    /// <summary>The account the job syncs; the account cannot be deleted while jobs reference it.</summary>
    public AccountId AccountId { get; set; }

    /// <summary>Friendly name shown to the user.</summary>
    public required string Name { get; set; }

    /// <summary>Absolute path of the local sync root.</summary>
    public required string LocalRootPath { get; set; }

    /// <summary>The provider's id for the remote sync root.</summary>
    public RemoteItemId RemoteRootId { get; set; }

    /// <summary>How the first synchronisation treats content on both sides; stored as the enum name; the migration that added the column gave existing rows <see cref="BootstrapMode.Merge"/>.</summary>
    public BootstrapMode BootstrapMode { get; set; }

    /// <summary>When <see langword="false"/> the job is kept but not synchronised.</summary>
    public bool Enabled { get; set; }

    /// <summary>When the job was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>When the job was last changed.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>Copies the domain job into its stored form.</summary>
    /// <param name="job">The job to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static SyncJobEntity From(SyncJob job)
        => new() { Id = job.Id, AccountId = job.AccountId, Name = job.Name, LocalRootPath = job.LocalRootPath, RemoteRootId = job.RemoteRootId, BootstrapMode = job.BootstrapMode, Enabled = job.Enabled, CreatedUtc = job.CreatedUtc, UpdatedUtc = job.UpdatedUtc };

    /// <summary>Builds the domain job from the stored row.</summary>
    /// <returns>An immutable job with the same values.</returns>
    public SyncJob ToJob() => new(Id, AccountId, Name, LocalRootPath, RemoteRootId, BootstrapMode, Enabled, CreatedUtc, UpdatedUtc);
}
