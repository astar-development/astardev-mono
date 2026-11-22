using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

internal static class ReconcilerStates
{
    internal const string KnownPath = "/Reports/Q1.xlsx";
    internal const string DefaultLocalRename = "/Local/Q1.xlsx";
    internal const string DefaultRemoteRename = "/Remote/Q1.xlsx";
    internal static readonly ContentFingerprint SharedFingerprint = new("shared");

    internal static readonly DateTimeOffset Stamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    internal static readonly ItemId ItemId = new(new Guid("6f1d2a30-4c1b-4c5e-9d7a-0a1b2c3d4e5f"));

    internal static KnownSnapshot Known() => new(new RelativePath(KnownPath), 10, Stamp, new ETag("e1"), null);

    internal static LocalSnapshot Local(string path = KnownPath, long size = 10, ContentFingerprint? fingerprint = null, DateTimeOffset? modified = null)
        => new(new RelativePath(path), size, modified ?? Stamp, null, fingerprint);

    internal static RemoteSnapshot Remote(string path = KnownPath, long size = 10, string etag = "e1", ContentFingerprint? fingerprint = null)
        => new(new RemoteItemId("r1"), new RelativePath(path), size, Stamp, new ETag(etag), null, fingerprint);

    internal static CanonicalItemState State(KnownSnapshot? known, LocalSnapshot? local, RemoteSnapshot? remote)
        => new(ItemId, known, local, remote);

    internal static CanonicalItemState Build(ChangeSet local, ChangeSet remote, string localPath = "", string remotePath = "", bool sameContent = false)
    {
        var created = (local | remote).HasFlag(ChangeSet.Created);
        var known = created ? null : Known();
        var localSnapshot = BuildLocal(local, known, localPath, sameContent);
        var remoteSnapshot = BuildRemote(remote, known, remotePath, sameContent);

        return State(known, localSnapshot, remoteSnapshot);
    }

    private static LocalSnapshot? BuildLocal(ChangeSet change, KnownSnapshot? known, string path, bool sameContent)
    {
        if (change.HasFlag(ChangeSet.Deleted) || (change == ChangeSet.None && known is null)) return null;
        var target = change.HasFlag(ChangeSet.Renamed) ? (path.Length > 0 ? path : DefaultLocalRename) : KnownPath;

        return Local(target, change.HasFlag(ChangeSet.Modified) ? 111 : 10, change == ChangeSet.Created && sameContent ? SharedFingerprint : null);
    }

    private static RemoteSnapshot? BuildRemote(ChangeSet change, KnownSnapshot? known, string path, bool sameContent)
    {
        if (change.HasFlag(ChangeSet.Deleted) || (change == ChangeSet.None && known is null)) return null;
        var target = change.HasFlag(ChangeSet.Renamed) ? (path.Length > 0 ? path : DefaultRemoteRename) : KnownPath;
        var modified = change.HasFlag(ChangeSet.Modified);
        var size = modified ? 222 : (change == ChangeSet.Created && !sameContent ? 20 : 10);

        return Remote(target, size, modified ? "e2" : "e1", change == ChangeSet.Created && sameContent ? SharedFingerprint : null);
    }
}
