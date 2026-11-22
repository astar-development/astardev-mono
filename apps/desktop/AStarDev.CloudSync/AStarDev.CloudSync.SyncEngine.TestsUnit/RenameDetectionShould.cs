using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class RenameDetectionShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ItemId First = new(new Guid("00000000-0000-0000-0000-000000000001"));
    private static readonly ItemId Second = new(new Guid("00000000-0000-0000-0000-000000000002"));

    public static TheoryData<bool, string?, string?, string?, string?, long, long, int, int, bool> Rules => new()
    {
        { true, "id1", "id1", null, null, 10, 99, 0, 5, true },
        { true, "id1", "id2", "h", "h", 10, 10, 0, 0, false },
        { true, null, null, "h", "h", 10, 10, 0, 0, true },
        { true, "id1", null, "h", "h", 10, 10, 0, 0, true },
        { true, null, null, "h", "h", 10, 11, 0, 0, false },
        { true, null, null, "h", "h", 10, 10, 0, 7, false },
        { true, null, null, "h", "g", 10, 10, 0, 0, false },
        { true, null, null, null, null, 10, 10, 0, 0, false },
        { true, null, null, "h", null, 10, 10, 0, 0, false },
        { false, "id1", "id1", null, null, 10, 99, 0, 5, true },
        { false, "id1", "id2", "h", "h", 10, 10, 0, 0, false },
        { false, null, null, "h", "h", 10, 10, 0, 0, false },
        { false, "id1", null, "h", "h", 10, 10, 0, 0, false }
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void apply_the_doc_04_rules(bool local, string? deletedIdentity, string? createdIdentity, string? deletedHash, string? createdHash, long deletedSize, long createdSize, int deletedSeconds, int createdSeconds, bool expected)
    {
        var deleted = new DeletedItem(First, Evidence("/a/f.txt", deletedSize, deletedSeconds, deletedHash, deletedIdentity));
        var created = Evidence("/b/f.txt", createdSize, createdSeconds, createdHash, createdIdentity);

        var renames = Detect(local, [deleted], [created]);

        (renames.Count == 1).ShouldBe(expected);
        if (expected) renames[0].ShouldBe(new DetectedRename(First, created.Path));
    }

    [Fact]
    public void prefer_a_missed_rename_to_a_false_rename()
    {
        var deleted = new DeletedItem(First, Evidence("/a/f.txt", 10, 0, "h", null));
        var lookalikes = new[] { Evidence("/b/f.txt", 10, 0, "h", null), Evidence("/c/f.txt", 10, 0, "h", null) };

        RenameDetector.DetectLocal([deleted], lookalikes).ShouldBeEmpty();
        RenameDetector.DetectLocal([deleted], [Evidence("/b/f.txt", 10, 0, null, null)]).ShouldBeEmpty();
        RenameDetector.DetectLocal([deleted], [Evidence("/b/f.txt", 10, 3, "other", null)]).ShouldBeEmpty();
    }

    [Fact]
    public void leave_two_deletes_matching_one_create_unpaired()
    {
        var deletes = new[]
        {
            new DeletedItem(First, Evidence("/a/f.txt", 10, 0, "h", null)),
            new DeletedItem(Second, Evidence("/a/g.txt", 10, 0, "h", null))
        };

        RenameDetector.DetectLocal(deletes, [Evidence("/b/f.txt", 10, 0, "h", null)]).ShouldBeEmpty();
    }

    [Fact]
    public void leave_two_creates_with_one_identity_unpaired()
    {
        var deleted = new DeletedItem(First, Evidence("/a/f.txt", 10, 0, null, "id1"));
        var creates = new[] { Evidence("/b/f.txt", 10, 0, null, "id1"), Evidence("/c/f.txt", 10, 0, null, "id1") };

        RenameDetector.DetectRemote([deleted], creates).ShouldBeEmpty();
    }

    [Fact]
    public void pair_independent_renames_in_one_pass()
    {
        var deletes = new[]
        {
            new DeletedItem(First, Evidence("/a/f.txt", 10, 0, null, "id1")),
            new DeletedItem(Second, Evidence("/a/g.txt", 20, 0, null, "id2"))
        };
        var creates = new[] { Evidence("/b/g.txt", 20, 0, null, "id2"), Evidence("/b/f.txt", 10, 0, null, "id1") };

        var renames = RenameDetector.DetectRemote(deletes, creates);

        renames.ShouldBe([new DetectedRename(First, new RelativePath("/b/f.txt")), new DetectedRename(Second, new RelativePath("/b/g.txt"))], ignoreOrder: true);
    }

    [Fact]
    public void not_pair_a_delete_and_create_at_the_same_path()
    {
        var deleted = new DeletedItem(First, Evidence("/a/f.txt", 10, 0, "h", null));

        RenameDetector.DetectLocal([deleted], [Evidence("/a/f.txt", 10, 0, "h", null)]).ShouldBeEmpty();
    }

    [Fact]
    public void yield_nothing_when_either_list_is_empty()
    {
        var deleted = new DeletedItem(First, Evidence("/a/f.txt", 10, 0, "h", null));

        RenameDetector.DetectLocal([deleted], []).ShouldBeEmpty();
        RenameDetector.DetectLocal([], [Evidence("/b/f.txt", 10, 0, "h", null)]).ShouldBeEmpty();
    }

    private static IReadOnlyList<DetectedRename> Detect(bool local, IReadOnlyList<DeletedItem> deleted, IReadOnlyList<RenameEvidence> created)
        => local ? RenameDetector.DetectLocal(deleted, created) : RenameDetector.DetectRemote(deleted, created);

    private static RenameEvidence Evidence(string path, long size, int seconds, string? hash, string? identity)
        => new(new RelativePath(path), size, Stamp.AddSeconds(seconds), hash is null ? null : new ContentFingerprint(hash), identity);
}
