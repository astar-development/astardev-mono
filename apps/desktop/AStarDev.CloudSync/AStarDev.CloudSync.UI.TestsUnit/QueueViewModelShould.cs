using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.CloudSync.UI.ViewModels.Screens;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class QueueViewModelShould
{
    [Fact]
    public void show_operations_in_every_status_with_a_count_per_status()
    {
        using var host = OperationScreensHost.Create();

        var queue = host.Screen<QueueViewModel>();

        queue.Rows.Select(row => row.Entry.Status).Distinct().Order().ShouldBe(Enum.GetValues<OperationStatus>());
        queue.StatusCounts.Select(count => count.Text).ShouldBe(["Pending: 2", "Running: 2", "Completed: 4", "Failed: 2"]);
    }

    [Fact]
    public void hide_the_filters_and_point_conflicts_to_their_own_screen()
    {
        using var host = OperationScreensHost.Create();

        var queue = host.Screen<QueueViewModel>();

        queue.ShowTools.ShouldBeFalse();
        queue.ConflictsNote.ShouldNotStartWith("!");
    }
}
