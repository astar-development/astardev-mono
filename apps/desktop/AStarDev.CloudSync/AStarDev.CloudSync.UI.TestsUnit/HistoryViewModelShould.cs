using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.CloudSync.UI.ViewModels.Screens;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class HistoryViewModelShould
{
    [Fact]
    public void show_only_completed_and_failed_operations()
    {
        using var host = OperationScreensHost.Create();

        var history = host.Screen<HistoryViewModel>();

        history.Rows.ShouldNotBeEmpty();
        history.Rows.ShouldAllBe(row => row.Entry.Status == OperationStatus.Completed || row.Entry.Status == OperationStatus.Failed);
    }

    [Fact]
    public void filter_by_date_range()
    {
        using var host = OperationScreensHost.Create();
        var history = host.Screen<HistoryViewModel>();

        history.DateFrom = new DateTime(2026, 11, 16);
        history.DateTo = new DateTime(2026, 11, 19);

        history.Rows.Select(row => row.Entry.Id).ShouldBe(["op-05", "op-06", "op-07", "op-08"]);
    }

    [Fact]
    public void filter_by_status()
    {
        using var host = OperationScreensHost.Create();
        var history = host.Screen<HistoryViewModel>();

        history.SelectedStatus = history.StatusChoices.Single(choice => choice.Value == nameof(OperationStatus.Failed));

        history.Rows.Select(row => row.Entry.Id).ShouldBe(["op-07", "op-10"]);
    }

    [Fact]
    public void start_on_the_history_tab_and_offer_an_audit_tab_flagged_as_pending()
    {
        using var host = OperationScreensHost.Create();

        var history = host.Screen<HistoryViewModel>();

        history.TabIndex.ShouldBe(0);
        history.AuditTabText.ShouldNotStartWith("!");
        history.HistoryTabText.ShouldNotStartWith("!");
        history.AuditPendingText.ShouldNotStartWith("!");
    }
}
