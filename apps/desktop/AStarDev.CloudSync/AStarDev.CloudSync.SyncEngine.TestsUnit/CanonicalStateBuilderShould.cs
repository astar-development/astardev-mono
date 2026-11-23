using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class CanonicalStateBuilderShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ItemId Remote1 = new(new Guid("00000000-0000-0000-0000-000000000001"));
    private static readonly ItemId LocalOnly1 = new(new Guid("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public void build_one_state_per_remote_row()
    {
        var states = CanonicalStateBuilder.Build([Remote(Remote1, "a.txt")], []);

        var state = states.ShouldHaveSingleItem();
        state.ItemId.ShouldBe(Remote1);
        state.Known.ShouldBeNull();
        state.Local.ShouldBeNull();
        state.Remote.ShouldNotBeNull();
    }

    [Fact]
    public void join_a_local_only_row_to_the_remote_row_at_the_same_path()
    {
        var states = CanonicalStateBuilder.Build([LocalRow(LocalOnly1, "a.txt"), Remote(Remote1, "a.txt")], []);

        var state = states.ShouldHaveSingleItem();
        state.ItemId.ShouldBe(Remote1);
        state.Local.ShouldNotBeNull();
        state.Remote.ShouldNotBeNull();
    }

    [Fact]
    public void keep_the_identity_that_carries_the_baseline_when_joining()
    {
        var known = new KnownEntry(LocalOnly1, Known("a.txt"));

        var state = CanonicalStateBuilder.Build([LocalRow(LocalOnly1, "a.txt"), Remote(Remote1, "a.txt")], [known]).ShouldHaveSingleItem();

        state.ItemId.ShouldBe(LocalOnly1);
        state.Known.ShouldBe(known.Snapshot);
    }

    [Fact]
    public void join_a_local_file_to_the_baseline_of_an_item_the_remote_deleted()
    {
        var known = new KnownEntry(Remote1, Known("a.txt"));

        var state = CanonicalStateBuilder.Build([LocalRow(LocalOnly1, "a.txt")], [known]).ShouldHaveSingleItem();

        state.ItemId.ShouldBe(Remote1);
        state.Local.ShouldNotBeNull();
        state.Remote.ShouldBeNull();
    }

    [Fact]
    public void use_the_local_file_at_the_baseline_path_when_the_remote_renamed_the_item()
    {
        var known = new KnownEntry(Remote1, Known("old.txt"));

        var state = CanonicalStateBuilder.Build([Remote(Remote1, "new.txt"), LocalRow(LocalOnly1, "old.txt")], [known]).ShouldHaveSingleItem();

        state.Local!.Path.ShouldBe(new RelativePath("old.txt"));
        state.Remote!.Path.ShouldBe(new RelativePath("new.txt"));
    }

    [Fact]
    public void treat_a_missing_local_file_with_a_proven_new_path_as_a_rename()
    {
        var known = new KnownEntry(Remote1, Known("old.txt") with { Fingerprint = new ContentFingerprint("h") });
        var moved = LocalRow(LocalOnly1, "new.txt", new ContentFingerprint("h"));

        var state = CanonicalStateBuilder.Build([Remote(Remote1, "old.txt"), moved], [known]).ShouldHaveSingleItem();

        state.ItemId.ShouldBe(Remote1);
        state.Local!.Path.ShouldBe(new RelativePath("new.txt"));
    }

    [Fact]
    public void leave_a_missing_file_and_a_new_file_apart_when_the_rename_is_not_proven()
    {
        var known = new KnownEntry(Remote1, Known("old.txt") with { Fingerprint = new ContentFingerprint("h") });
        var other = LocalRow(LocalOnly1, "new.txt", new ContentFingerprint("different"));

        var states = CanonicalStateBuilder.Build([Remote(Remote1, "old.txt"), other], [known]);

        states.Count.ShouldBe(2);
        states.Single(state => state.ItemId == Remote1).Local.ShouldBeNull();
    }

    [Fact]
    public void carry_whether_the_disk_was_observed_for_the_item()
    {
        var known = Known("a.txt");

        var unobserved = CanonicalStateBuilder.Build([Remote(Remote1, "a.txt") with { LocalObserved = false }], [new KnownEntry(Remote1, known)]);
        var deleted = CanonicalStateBuilder.Build([Remote(Remote1, "a.txt")], [new KnownEntry(Remote1, known)]);

        unobserved.ShouldHaveSingleItem().LocalObserved.ShouldBeFalse();
        deleted.ShouldHaveSingleItem().LocalObserved.ShouldBeTrue();
    }

    [Fact]
    public void ignore_a_local_only_row_whose_file_is_gone_and_never_had_a_baseline()
        => CanonicalStateBuilder.Build([new StoredItem(LocalOnly1, new RemoteItemId(string.Empty), new RelativePath("a.txt"), null, null)], []).ShouldBeEmpty();

    private static KnownSnapshot Known(string path) => new(new RelativePath(path), 10, Stamp, null, null);

    private static StoredItem Remote(ItemId id, string path)
        => new(id, new RemoteItemId("r-" + path), new RelativePath(path), null, new RemoteSnapshot(new RemoteItemId("r-" + path), new RelativePath(path), 10, Stamp, new ETag(string.Empty), null));

    private static StoredItem LocalRow(ItemId id, string path, ContentFingerprint? fingerprint = null)
        => new(id, new RemoteItemId(string.Empty), new RelativePath(path), new LocalSnapshot(new RelativePath(path), 10, Stamp, null, fingerprint), null);
}
