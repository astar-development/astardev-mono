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

    /// <summary>When <see langword="false"/> the job is kept but not synchronised.</summary>
    public bool Enabled { get; set; }

    /// <summary>When the job was created.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>When the job was last changed.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
