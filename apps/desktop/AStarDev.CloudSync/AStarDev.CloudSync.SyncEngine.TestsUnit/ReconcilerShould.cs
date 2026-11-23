using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class ReconcilerShould
{
    private const int None = (int)ChangeSet.None;
    private const int Created = (int)ChangeSet.Created;
    private const int Deleted = (int)ChangeSet.Deleted;
    private const int Renamed = (int)ChangeSet.Renamed;
    private const int Modified = (int)ChangeSet.Modified;
    private const string Same = "/Shared/Q1.xlsx";

    public static TheoryData<int, int, string, string, bool, string> DecisionTable => new()
    {
        { None, None, "", "", false, "NoOp" },
        { Created, None, "", "", false, "CreateRemote" },
        { None, Created, "", "", false, "CreateLocal" },
        { Modified, None, "", "", false, "UpdateRemote" },
        { None, Modified, "", "", false, "UpdateLocal" },
        { Renamed, None, "", "", false, "RenameRemote" },
        { None, Renamed, "", "", false, "RenameLocal" },
        { Deleted, None, "", "", false, "DeleteRemote" },
        { None, Deleted, "", "", false, "DeleteLocal" },
        { Deleted, Deleted, "", "", false, "NoOp" },
        { Modified, Modified, "", "", false, "Conflict:ModifyModify" },
        { Renamed, Renamed, Same, Same, false, "NoOp" },
        { Renamed, Renamed, "", "", false, "Conflict:RenameRename" },
        { Renamed, Modified, "", "", false, "Merge" },
        { Modified, Renamed, "", "", false, "Merge" },
        { Renamed, Deleted, "", "", false, "Conflict:KeepDelete" },
        { Deleted, Renamed, "", "", false, "Conflict:KeepDelete" },
        { Modified, Deleted, "", "", false, "Conflict:KeepDelete" },
        { Deleted, Modified, "", "", false, "Conflict:KeepDelete" },
        { Created, Created, "", "", true, "NoOp" },
        { Created, Created, "", "", false, "Conflict:CreateCreate" },
        { Renamed | Modified, None, "", "", false, "RenameRemote+UpdateRemote" },
        { None, Renamed | Modified, "", "", false, "RenameLocal+UpdateLocal" },
        { Renamed | Modified, Renamed, Same, Same, false, "UpdateRemote" },
        { Renamed | Modified, Modified, "", "", false, "Conflict:ModifyModify" },
        { Renamed | Modified, Deleted, "", "", false, "Conflict:KeepDelete" }
    };

    [Theory]
    [MemberData(nameof(DecisionTable))]
    public void apply_the_decision_table(int local, int remote, string localPath, string remotePath, bool sameContent, string expected)
    {
        var state = ReconcilerStates.Build((ChangeSet)local, (ChangeSet)remote, localPath, remotePath, sameContent);

        Describe(new Reconciler().Reconcile(state)).ShouldBe(expected);
    }

    [Fact]
    public void merge_a_rename_with_a_content_change()
    {
        var deleted = new DeletedItem(ReconcilerStates.ItemId, new RenameEvidence(new RelativePath(ReconcilerStates.KnownPath), 10, ReconcilerStates.Stamp, null, "inode-1"));
        var created = new RenameEvidence(new RelativePath("/Archive/Q1.xlsx"), 10, ReconcilerStates.Stamp, null, "inode-1");
        var rename = RenameDetector.DetectLocal([deleted], [created]).ShouldHaveSingleItem();
        var local = ReconcilerStates.Local(rename.To.Value);
        var remote = ReconcilerStates.Remote(size: 222, etag: "e2");

        var decision = new Reconciler().Reconcile(ReconcilerStates.State(ReconcilerStates.Known(), local, remote));

        Describe(decision).ShouldBe("Merge");
    }

    [Fact]
    public void merge_a_detected_remote_rename_with_a_local_edit()
    {
        var deleted = new DeletedItem(ReconcilerStates.ItemId, new RenameEvidence(new RelativePath(ReconcilerStates.KnownPath), 10, ReconcilerStates.Stamp, null, "r1"));
        var created = new RenameEvidence(new RelativePath("/Archive/Q1.xlsx"), 10, ReconcilerStates.Stamp, null, "r1");
        var rename = RenameDetector.DetectRemote([deleted], [created]).ShouldHaveSingleItem();
        var remote = ReconcilerStates.Remote(rename.To.Value);
        var local = ReconcilerStates.Local(size: 111);

        var decision = new Reconciler().Reconcile(ReconcilerStates.State(ReconcilerStates.Known(), local, remote));

        Describe(decision).ShouldBe("Merge");
    }

    [Fact]
    public void produce_a_conflict_for_modify_modify()
    {
        var state = ReconcilerStates.Build(ChangeSet.Modified, ChangeSet.Modified);

        var decision = new Reconciler().Reconcile(state);

        decision.Value.ShouldBeOfType<Conflicted>().Reason.ShouldBe(ConflictReason.ModifyModify);
    }

    [Fact]
    public void never_choose_an_outcome_that_discards_content()
    {
        var sides = new[] { None, Created, Deleted, Renamed, Modified, Renamed | Modified }.Select(value => (ChangeSet)value);
        var reconciler = new Reconciler();
        var checkedStates = 0;

        foreach (var local in sides)
        foreach (var remote in sides)
        foreach (var sameContent in new[] { true, false })
        {
            if (local.HasFlag(ChangeSet.Created) && remote is not (ChangeSet.None or ChangeSet.Created)) continue;
            if (remote.HasFlag(ChangeSet.Created) && local is not (ChangeSet.None or ChangeSet.Created)) continue;
            var state = ReconcilerStates.Build(local, remote, "", "", sameContent);
            var steps = (reconciler.Reconcile(state).Value as Resolved)?.Steps ?? [];

            if (steps.Contains(SyncDecisionType.UpdateLocal)) local.HasFlag(ChangeSet.Modified).ShouldBeFalse($"{local}/{remote} overwrites local edits");
            if (steps.Contains(SyncDecisionType.UpdateRemote)) remote.HasFlag(ChangeSet.Modified).ShouldBeFalse($"{local}/{remote} overwrites remote edits");
            if (steps.Contains(SyncDecisionType.DeleteLocal)) local.HasFlag(ChangeSet.Modified).ShouldBeFalse($"{local}/{remote} deletes local edits");
            if (steps.Contains(SyncDecisionType.DeleteRemote)) remote.HasFlag(ChangeSet.Modified).ShouldBeFalse($"{local}/{remote} deletes remote edits");
            if (steps.Contains(SyncDecisionType.DeleteLocal)) local.HasFlag(ChangeSet.Renamed).ShouldBeFalse($"{local}/{remote} deletes a renamed local item");
            if (steps.Contains(SyncDecisionType.DeleteRemote)) remote.HasFlag(ChangeSet.Renamed).ShouldBeFalse($"{local}/{remote} deletes a renamed remote item");
            if (steps.Contains(SyncDecisionType.CreateLocal)) (state.Local is null).ShouldBeTrue();
            if (steps.Contains(SyncDecisionType.CreateRemote)) (state.Remote is null).ShouldBeTrue();
            checkedStates++;
        }

        checkedStates.ShouldBeGreaterThan(20);
    }

    [Fact]
    public void ignore_a_touch_that_leaves_the_fingerprint_unchanged()
    {
        var fingerprint = new ContentFingerprint("h1");
        var known = ReconcilerStates.Known() with { Fingerprint = fingerprint };
        var local = ReconcilerStates.Local(fingerprint: fingerprint, modified: DateTimeOffset.UnixEpoch);
        var state = ReconcilerStates.State(known, local, ReconcilerStates.Remote());

        Describe(new Reconciler().Reconcile(state)).ShouldBe("NoOp");
    }

    [Fact]
    public void detect_a_local_edit_by_fingerprint_when_the_size_is_unchanged()
    {
        var known = ReconcilerStates.Known() with { Fingerprint = new ContentFingerprint("h1") };
        var local = ReconcilerStates.Local(fingerprint: new ContentFingerprint("h2"));
        var state = ReconcilerStates.State(known, local, ReconcilerStates.Remote());

        Describe(new Reconciler().Reconcile(state)).ShouldBe("UpdateRemote");
    }

    [Fact]
    public void report_nothing_to_do_for_an_item_missing_everywhere()
        => Describe(new Reconciler().Reconcile(ReconcilerStates.State(null, null, null))).ShouldBe("NoOp");

    [Fact]
    public void leave_a_known_item_alone_when_the_disk_was_never_observed_for_it()
        => Describe(new Reconciler().Reconcile(ReconcilerStates.State(ReconcilerStates.Known(), null, ReconcilerStates.Remote()) with { LocalObserved = false })).ShouldBe("NoOp");

    [Fact]
    public void treat_creations_with_equal_path_size_and_time_but_no_proof_as_a_conflict()
    {
        var state = ReconcilerStates.State(null, ReconcilerStates.Local("/A.txt"), ReconcilerStates.Remote("/A.txt"));

        Describe(new Reconciler().Reconcile(state)).ShouldBe("Conflict:CreateCreate");
    }

    [Fact]
    public void treat_creations_with_equal_fingerprints_as_the_same_item()
    {
        var state = ReconcilerStates.State(null, ReconcilerStates.Local("/A.txt", fingerprint: new ContentFingerprint("h")), ReconcilerStates.Remote("/A.txt", fingerprint: new ContentFingerprint("h")));

        Describe(new Reconciler().Reconcile(state)).ShouldBe("NoOp");
    }

    [Fact]
    public void treat_creations_with_different_fingerprints_as_a_conflict()
    {
        var state = ReconcilerStates.State(null, ReconcilerStates.Local("/A.txt", fingerprint: new ContentFingerprint("h")), ReconcilerStates.Remote("/A.txt", fingerprint: new ContentFingerprint("g")));

        Describe(new Reconciler().Reconcile(state)).ShouldBe("Conflict:CreateCreate");
    }

    [Fact]
    public void treat_creations_at_different_paths_as_a_conflict()
    {
        var state = ReconcilerStates.State(null, ReconcilerStates.Local("/A.txt"), ReconcilerStates.Remote("/B.txt"));

        Describe(new Reconciler().Reconcile(state)).ShouldBe("Conflict:CreateCreate");
    }

    private static string Describe(SyncDecision decision)
        => decision switch
        {
            Resolved resolved => string.Join("+", resolved.Steps),
            Conflicted conflicted => $"Conflict:{conflicted.Reason}"
        };
}
