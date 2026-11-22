using AStarDev.CloudSync.Domain;
using static AStarDev.CloudSync.SyncEngine.TestsUnit.ReconcilerStates;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class OperationPlannerShould
{
    private static readonly ContentFingerprint LocalFingerprint = new("local");

    [Fact]
    public void plan_nothing_for_a_no_op()
        => OperationPlanner.Plan(State(Known(), Local(), Remote()), new Resolved([SyncDecisionType.NoOp])).ShouldBe(ItemPlan.Nothing);

    [Fact]
    public void plan_a_download_that_baselines_the_remote_state()
    {
        var plan = OperationPlanner.Plan(State(null, null, Remote("/a.txt", 7, "e9")), new Resolved([SyncDecisionType.CreateLocal]));

        plan.Operations.ShouldBe([new PlannedOperation(OperationType.DownloadFile, OperationPriority.Background)]);
        plan.Known.ShouldBe(new KnownSnapshot(new RelativePath("/a.txt"), 7, Stamp, new ETag("e9"), null));
    }

    [Fact]
    public void plan_an_upload_that_baselines_the_local_state()
    {
        var plan = OperationPlanner.Plan(State(Known(), Local(size: 99, fingerprint: LocalFingerprint), Remote()), new Resolved([SyncDecisionType.UpdateRemote]));

        plan.Operations.ShouldBe([new PlannedOperation(OperationType.UploadFile, OperationPriority.Background)]);
        plan.Known.ShouldBe(new KnownSnapshot(new RelativePath(KnownPath), 99, Stamp, null, LocalFingerprint));
    }

    [Fact]
    public void plan_a_delete_that_removes_the_baseline()
    {
        var plan = OperationPlanner.Plan(State(Known(), null, Remote()), new Resolved([SyncDecisionType.DeleteRemote]));

        plan.Operations.ShouldBe([new PlannedOperation(OperationType.DeleteRemote, OperationPriority.Background)]);
        plan.Known.ShouldBeNull();
    }

    [Fact]
    public void plan_a_rename_only_with_the_known_content_at_the_new_path()
    {
        var plan = OperationPlanner.Plan(State(Known(), Local(DefaultLocalRename), Remote()), new Resolved([SyncDecisionType.RenameRemote]));

        plan.Operations.ShouldBe([new PlannedOperation(OperationType.RenameRemote, OperationPriority.Interactive)]);
        plan.Known.ShouldBe(Known() with { Path = new RelativePath(DefaultLocalRename) });
    }

    [Fact]
    public void recover_a_local_rename_and_a_remote_update_from_a_merge()
    {
        var state = State(Known(), Local(DefaultLocalRename), Remote(size: 50, etag: "e2"));

        var plan = OperationPlanner.Plan(state, new Resolved([SyncDecisionType.Merge]));

        plan.Operations.Select(operation => operation.Type).ShouldBe([OperationType.RenameRemote, OperationType.DownloadFile]);
        plan.Known.ShouldBe(new KnownSnapshot(new RelativePath(DefaultLocalRename), 50, Stamp, new ETag("e2"), null));
    }

    [Fact]
    public void recover_a_remote_rename_and_a_local_update_from_a_merge()
    {
        var state = State(Known(), Local(size: 50, fingerprint: LocalFingerprint), Remote(DefaultRemoteRename));

        var plan = OperationPlanner.Plan(state, new Resolved([SyncDecisionType.Merge]));

        plan.Operations.Select(operation => operation.Type).ShouldBe([OperationType.RenameLocal, OperationType.UploadFile]);
        plan.Known.ShouldBe(new KnownSnapshot(new RelativePath(DefaultRemoteRename), 50, Stamp, null, LocalFingerprint));
    }

    [Fact]
    public void order_a_rename_before_the_content_update_by_priority()
    {
        var plan = OperationPlanner.Plan(State(Known(), Local(DefaultLocalRename), Remote(size: 50, etag: "e2")), new Resolved([SyncDecisionType.Merge]));

        plan.Operations.Select(operation => operation.Priority).ShouldBe([OperationPriority.Interactive, OperationPriority.Background]);
    }

    [Fact]
    public void name_every_conflict_reason_as_a_conflict_type()
        => Enum.GetNames<ConflictReason>().ShouldBe(Enum.GetNames<ConflictType>());
}
