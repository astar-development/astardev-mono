using AStarDev.CloudSync.Domain;
using static AStarDev.CloudSync.SyncEngine.TestsUnit.ReconcilerStates;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class ResolutionPlannerShould
{
    [Fact]
    public void upload_the_local_file_when_keeping_local()
        => ResolutionPlanner.Plan(ConflictType.ModifyModify, State(Known(), Local(), Remote()), ConflictResolution.KeepLocal).Steps.ShouldBe([SyncDecisionType.UpdateRemote]);

    [Fact]
    public void download_the_remote_file_when_keeping_remote()
        => ResolutionPlanner.Plan(ConflictType.ModifyModify, State(Known(), Local(), Remote()), ConflictResolution.KeepRemote).Steps.ShouldBe([SyncDecisionType.UpdateLocal]);

    [Fact]
    public void move_the_item_for_a_rename_conflict()
    {
        var state = State(Known(), Local(DefaultLocalRename), Remote(DefaultRemoteRename));

        ResolutionPlanner.Plan(ConflictType.RenameRename, state, ConflictResolution.KeepLocal).Steps.ShouldBe([SyncDecisionType.RenameRemote]);
        ResolutionPlanner.Plan(ConflictType.RenameRename, state, ConflictResolution.KeepRemote).Steps.ShouldBe([SyncDecisionType.RenameLocal]);
    }

    [Fact]
    public void delete_the_other_side_only_when_the_chosen_side_has_no_file()
    {
        ResolutionPlanner.Plan(ConflictType.KeepDelete, State(Known(), null, Remote()), ConflictResolution.KeepLocal).Steps.ShouldBe([SyncDecisionType.DeleteRemote]);
        ResolutionPlanner.Plan(ConflictType.KeepDelete, State(Known(), Local(), null), ConflictResolution.KeepRemote).Steps.ShouldBe([SyncDecisionType.DeleteLocal]);
    }

    [Fact]
    public void keep_the_surviving_file_when_keeping_both_and_one_side_is_gone()
    {
        ResolutionPlanner.Plan(ConflictType.KeepDelete, State(Known(), Local(), null), ConflictResolution.KeepBoth).Steps.ShouldBe([SyncDecisionType.UpdateRemote]);
        ResolutionPlanner.Plan(ConflictType.KeepDelete, State(Known(), null, Remote()), ConflictResolution.KeepBoth).Steps.ShouldBe([SyncDecisionType.UpdateLocal]);
    }

    [Fact]
    public void apply_the_remote_side_after_the_local_copy_when_keeping_both()
    {
        var state = State(Known(), Local(), Remote());

        ResolutionPlanner.Plan(ConflictType.ModifyModify, state, ConflictResolution.KeepBoth).Steps.ShouldBe([SyncDecisionType.UpdateLocal]);
        ResolutionPlanner.NeedsCopy(state).ShouldBeTrue();
        ResolutionPlanner.NeedsCopy(State(Known(), Local(), null)).ShouldBeFalse();
    }
}
