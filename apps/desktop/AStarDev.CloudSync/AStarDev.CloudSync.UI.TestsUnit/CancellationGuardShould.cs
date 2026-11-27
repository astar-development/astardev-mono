namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class CancellationGuardShould
{
    [Fact]
    public void swallow_a_task_cancellation() => CancellationGuard.Swallows(new TaskCanceledException()).ShouldBeTrue();

    [Fact]
    public void not_swallow_any_other_exception() => CancellationGuard.Swallows(new InvalidOperationException()).ShouldBeFalse();
}
