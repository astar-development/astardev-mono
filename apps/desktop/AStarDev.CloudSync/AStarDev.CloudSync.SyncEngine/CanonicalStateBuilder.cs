using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Turns stored item rows and baselines into one <see cref="CanonicalItemState"/> per logical item. Pure: no I/O, no state.</summary>
internal static class CanonicalStateBuilder
{
    /// <summary>
    /// Merges the rows into logical items. A local-only row joins the remote row at its path, or at the baseline path of a remotely renamed row, so one item never yields both an upload and a download.
    /// A baseline whose row was removed by a remote delete joins the local-only row at its path. A known item missing locally and a new local file that rename detection proves the same item become one renamed item; the missing item's last filesystem identity comes from the rows stored at its baseline path.
    /// </summary>
    /// <param name="items">Every stored file row of the job.</param>
    /// <param name="known">The baselines of the job.</param>
    /// <returns>One state per logical item. A joined item keeps the identity that carries a baseline, otherwise the identity of its remote row.</returns>
    public static IReadOnlyList<CanonicalItemState> Build(IReadOnlyList<StoredItem> items, IReadOnlyList<KnownEntry> known)
    {
        var baselines = known.ToDictionary(entry => entry.ItemId, entry => entry.Snapshot);
        var rowIds = items.Select(item => item.ItemId).ToHashSet();
        var anchored = items.Where(HasRemoteId).Select(item => new Logical(item.ItemId, item.Path, item.Local, item.Remote, item.LocalObserved)).ToList();
        var orphans = known.Where(entry => !rowIds.Contains(entry.ItemId)).Select(entry => new Logical(entry.ItemId, entry.Snapshot.Path, null, null, true)).ToList();
        var standalone = new List<Logical>();

        foreach (var localOnly in items.Where(item => !HasRemoteId(item) && (item.Local is not null || baselines.ContainsKey(item.ItemId))))
        {
            var target = FindTarget(anchored, orphans, baselines, localOnly.Path);
            if (target is null) standalone.Add(new Logical(localOnly.ItemId, localOnly.Path, localOnly.Local, null, localOnly.LocalObserved));
            else Join(target, localOnly, baselines);
        }

        var logical = anchored.Concat(orphans).Concat(standalone).ToList();
        ApplyLocalRenames(logical, standalone, baselines, items.Where(item => item.LastLocalIdentity is not null).GroupBy(item => item.Path).ToDictionary(group => group.Key, group => group.First().LastLocalIdentity!.Value));

        return logical.Select(item => new CanonicalItemState(item.Id, baselines.GetValueOrDefault(item.Id), item.Local, item.Remote, item.LocalObserved)).ToList();
    }

    private static bool HasRemoteId(StoredItem item) => item.RemoteItemId.Value.Length > 0;

    private static Logical? FindTarget(List<Logical> anchored, List<Logical> orphans, Dictionary<ItemId, KnownSnapshot> baselines, RelativePath path)
        => anchored.FirstOrDefault(item => item.Path == path)
            ?? anchored.FirstOrDefault(item => baselines.TryGetValue(item.Id, out var baseline) && baseline.Path == path)
            ?? orphans.FirstOrDefault(item => item.Path == path);

    private static void Join(Logical target, StoredItem localOnly, Dictionary<ItemId, KnownSnapshot> baselines)
    {
        target.Local = localOnly.Local ?? target.Local;
        target.LocalObserved |= localOnly.LocalObserved;
        if (baselines.ContainsKey(localOnly.ItemId) && !baselines.ContainsKey(target.Id)) target.Id = localOnly.ItemId;
    }

    private static void ApplyLocalRenames(List<Logical> logical, List<Logical> standalone, Dictionary<ItemId, KnownSnapshot> baselines, Dictionary<RelativePath, LocalIdentity> lastIdentities)
    {
        var deleted = logical
            .Where(item => item.Local is null && item.Remote is not null && baselines.ContainsKey(item.Id))
            .Select(item => new DeletedItem(item.Id, ToEvidence(baselines[item.Id], lastIdentities)))
            .ToList();
        var created = standalone
            .Where(item => item.Local is not null && item.Remote is null && !baselines.ContainsKey(item.Id))
            .ToList();
        var evidence = created.Select(item => ToEvidence(item.Local!)).ToList();

        foreach (var rename in RenameDetector.DetectLocal(deleted, evidence))
        {
            var moved = created.First(item => item.Local!.Path == rename.To);
            logical.First(item => item.Id == rename.ItemId).Local = moved.Local;
            logical.Remove(moved);
        }
    }

    private static RenameEvidence ToEvidence(KnownSnapshot snapshot, Dictionary<RelativePath, LocalIdentity> lastIdentities)
        => new(snapshot.Path, snapshot.Size, snapshot.ModifiedUtc, snapshot.Fingerprint, lastIdentities.TryGetValue(snapshot.Path, out var identity) ? identity.Value : null);

    private static RenameEvidence ToEvidence(LocalSnapshot snapshot) => new(snapshot.Path, snapshot.Size, snapshot.ModifiedUtc, snapshot.Fingerprint, snapshot.Identity?.Value);

    private sealed class Logical(ItemId id, RelativePath path, LocalSnapshot? local, RemoteSnapshot? remote, bool localObserved)
    {
        public ItemId Id { get; set; } = id;

        public RelativePath Path { get; } = path;

        public LocalSnapshot? Local { get; set; } = local;

        public RemoteSnapshot? Remote { get; } = remote;

        public bool LocalObserved { get; set; } = localObserved;
    }
}
