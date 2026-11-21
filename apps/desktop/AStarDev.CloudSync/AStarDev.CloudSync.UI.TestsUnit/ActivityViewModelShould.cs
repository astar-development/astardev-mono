using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.CloudSync.UI.ViewModels.Screens;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ActivityViewModelShould
{
    [Fact]
    public void show_only_pending_and_running_operations()
    {
        using var host = OperationScreensHost.Create();

        var activity = host.Screen<ActivityViewModel>();

        activity.Rows.ShouldNotBeEmpty();
        activity.Rows.ShouldAllBe(row => row.Entry.Status == OperationStatus.Pending || row.Entry.Status == OperationStatus.Running);
    }

    [Fact]
    public void filter_by_operation_type()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();

        activity.SelectedType = activity.TypeChoices.Single(choice => choice.Value == nameof(OperationType.Upload));

        activity.Rows.ShouldNotBeEmpty();
        activity.Rows.ShouldAllBe(row => row.Entry.Type == OperationType.Upload);
    }

    [Fact]
    public void filter_by_provider_status_and_path_together()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();

        activity.SelectedProvider = activity.ProviderChoices.Single(choice => choice.Value == "OneDrive (work)");
        activity.SelectedStatus = activity.StatusChoices.Single(choice => choice.Value == nameof(OperationStatus.Pending));
        activity.PathText = "OLD";

        activity.Rows.Select(row => row.Entry.Id).ShouldBe(["op-03"]);
    }

    [Fact]
    public void report_no_rows_and_restore_every_row_when_filters_are_cleared()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();
        var total = activity.Rows.Count;

        activity.PathText = "no such path";
        activity.HasNoRows.ShouldBeTrue();
        activity.ClearFilters.Execute(null);

        activity.HasNoRows.ShouldBeFalse();
        activity.Rows.Count.ShouldBe(total);
    }

    [Fact]
    public void localise_its_sample_label_export_explanation_and_retention()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();

        activity.SampleDataText.ShouldNotStartWith("!");
        activity.ExportDisabledReason.ShouldNotStartWith("!");
        activity.ExportText.ShouldNotStartWith("!");
        activity.RetentionText.ShouldNotStartWith("!");
        activity.Title.ShouldNotStartWith("!");
    }

    [Fact]
    public void format_row_dates_with_the_culture_service()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();

        var row = activity.Rows.First();

        row.Date.ShouldBe("20/11/2026");
        row.Time.ShouldBe("12:00");
    }

    [Fact]
    public void open_the_operation_named_by_a_navigation_target()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();

        activity.OnNavigatedTo("op-02");

        activity.Details.Row!.Entry.Id.ShouldBe("op-02");
    }
}
