using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of a <see cref="SyncScope"/>. Keyed by job and folder, so a job holds each folder once.</summary>
internal sealed class SyncScopeEntity
{
    /// <summary>The job the folder belongs to; the scopes go when the job goes.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary>The provider's id for the selected folder.</summary>
    public RemoteItemId RemoteRootId { get; set; }

    /// <summary>Where the folder sits from the drive root; empty text when it is not known.</summary>
    public RelativePath Path { get; set; }

    /// <summary>Copies the domain scope into its stored form.</summary>
    /// <param name="scope">The scope to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static SyncScopeEntity From(SyncScope scope) => new() { SyncJobId = scope.SyncJobId, RemoteRootId = scope.RemoteRootId, Path = new RelativePath(scope.Path.Value ?? string.Empty) };

    /// <summary>Builds the domain scope from the stored row.</summary>
    /// <returns>An immutable scope with the same values; an empty stored path becomes the default path, so a scope added without a path reads back equal.</returns>
    public SyncScope ToScope() => new(SyncJobId, RemoteRootId, string.IsNullOrEmpty(Path.Value) ? default : Path);
}
