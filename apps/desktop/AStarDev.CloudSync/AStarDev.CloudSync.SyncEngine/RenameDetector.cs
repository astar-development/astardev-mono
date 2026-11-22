using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Pairs deleted and created items of one side into renames. Pure: no I/O, no state. When a pairing is not proven or not unique it yields nothing, so the delete and the create stay as they are.</summary>
internal static class RenameDetector
{
    /// <summary>Finds local renames. Equal filesystem identities pair items outright; when either identity is missing, a pair needs the same fingerprint, size and modified time.</summary>
    /// <param name="deleted">Known items that are gone locally.</param>
    /// <param name="created">Local items that match no known item.</param>
    /// <returns>Only the unique, proven pairs. A delete or create with several candidates, or none, is left out.</returns>
    public static IReadOnlyList<DetectedRename> DetectLocal(IReadOnlyList<DeletedItem> deleted, IReadOnlyList<RenameEvidence> created)
        => Pair(deleted, created, IsLocalMatch);

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

    private static bool IsLocalMatch(RenameEvidence known, RenameEvidence current)
    {
        if (known.Identity is not null && current.Identity is not null) return known.Identity == current.Identity;

        return known.Fingerprint is { } fingerprint
            && fingerprint == current.Fingerprint
            && known.Size == current.Size
            && known.ModifiedUtc == current.ModifiedUtc;
    }
}
