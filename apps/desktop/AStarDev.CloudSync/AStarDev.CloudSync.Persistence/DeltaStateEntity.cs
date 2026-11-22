using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The provider's change bookmark for one scope. At most one row per scope; it goes when the scope goes.</summary>
internal sealed class DeltaStateEntity
{
    /// <summary>The job of the scope; part of the primary key.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary>The scope's folder; part of the primary key.</summary>
    public RemoteItemId RemoteRootId { get; set; }

    /// <summary>The bookmark for "changes since the last applied page"; replaced in the same write that applies the changes.</summary>
    public DeltaToken DeltaToken { get; set; }

    /// <summary>When the token was last written.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
