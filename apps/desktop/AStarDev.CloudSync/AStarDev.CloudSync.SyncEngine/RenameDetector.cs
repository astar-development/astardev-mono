using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Pairs deleted and created items of one side into renames. Pure: no I/O, no state. When a pairing is not proven or not unique it yields nothing, so the delete and the create stay as they are.</summary>
internal static class RenameDetector
{
    /// <summary>
    /// Finds local renames in two passes. First, equal filesystem identities pair items, but only when the size is also equal: a reused inode holding other content is not a rename.
    /// Then the items still unpaired are matched by fingerprint, size and modified time, but only when at least one identity is missing; two known, different identities never match.
    /// </summary>
    /// <param name="deleted">Known items that are gone locally.</param>
    /// <param name="created">Local items that match no known item.</param>
    /// <returns>Only the unique, proven pairs. A delete or create with several candidates, or none, is left out.</returns>
    public static IReadOnlyList<DetectedRename> DetectLocal(IReadOnlyList<DeletedItem> deleted, IReadOnlyList<RenameEvidence> created)
    {
        var byIdentity = Pair(deleted, created, IsIdentityMatch);
        var remainingDeleted = deleted.Where(item => byIdentity.All(rename => rename.ItemId != item.ItemId)).ToList();
        var remainingCreated = created.Where(target => byIdentity.All(rename => rename.To != target.Path)).ToList();

        return [.. byIdentity, .. Pair(remainingDeleted, remainingCreated, IsFingerprintMatch)];
    }

    /// <summary>Finds remote renames. Only equal provider item ids pair items; content evidence is never used.</summary>
    /// <param name="deleted">Known items that are gone remotely, carrying their provider item id as identity.</param>
    /// <param name="created">Remote items that match no known item, carrying their provider item id as identity.</param>
    /// <returns>Only the unique, proven pairs. A delete or create with several candidates, or none, is left out.</returns>
    public static IReadOnlyList<DetectedRename> DetectRemote(IReadOnlyList<DeletedItem> deleted, IReadOnlyList<RenameEvidence> created)
        => Pair(deleted, created, IsRemoteMatch);

    private static IReadOnlyList<DetectedRename> Pair(IReadOnlyList<DeletedItem> deleted, IReadOnlyList<RenameEvidence> created, Func<RenameEvidence, RenameEvidence, bool> isMatch)
    {
        var candidates = deleted
            .SelectMany(item => created.Where(target => target.Path != item.Evidence.Path && isMatch(item.Evidence, target)).Select(target => (Item: item, Target: target)))
            .ToList();

        return candidates
            .Where(pair => candidates.Count(other => other.Item == pair.Item) == 1 && candidates.Count(other => other.Target == pair.Target) == 1)
            .Select(pair => new DetectedRename(pair.Item.ItemId, pair.Target.Path))
            .ToList();
    }

    private static bool IsRemoteMatch(RenameEvidence known, RenameEvidence current)
        => known.Identity is not null && known.Identity == current.Identity;

    private static bool IsIdentityMatch(RenameEvidence known, RenameEvidence current)
        => known.Identity is not null && known.Identity == current.Identity && known.Size == current.Size;

    private static bool IsFingerprintMatch(RenameEvidence known, RenameEvidence current)
        => (known.Identity is null || current.Identity is null)
            && known.Fingerprint is { } fingerprint
            && fingerprint == current.Fingerprint
            && known.Size == current.Size
            && ModifiedTimes.Same(known.ModifiedUtc, current.ModifiedUtc);
}
