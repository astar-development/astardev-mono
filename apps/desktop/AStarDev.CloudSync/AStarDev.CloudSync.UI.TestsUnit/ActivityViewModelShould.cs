using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.CloudSync.UI.ViewModels.Screens;

using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ActivityViewModelShould
{
    [Fact]
    public void render_daemon_progress_without_calculating_it()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();
        var started = new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

        host.Services.GetRequiredService<ConnectionViewModel>().Activities.Add(new ActivityView(Guid.NewGuid(), "OneDrive", ActivityState.InitialIndexing, 183442, 702144, 42.5, 1020, started, started, null, false));

        var row = activity.DaemonActivities.ShouldHaveSingleItem();
        activity.HasDaemonActivities.ShouldBeTrue();
        row.Title.ShouldBe("Initial indexing: OneDrive");
        row.ProgressText.ShouldBe("183,442 / 702,144 items indexed (42.5%)");
        row.Percent.ShouldBe(42.5);
        row.EtaText.ShouldBe("Estimated time left: 17 minutes");
        row.RetryText.ShouldBeNull();
    }

    [Fact]
    public void show_waiting_with_the_retry_seconds_and_what_needs_the_user()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();
        var started = new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

        host.Services.GetRequiredService<ConnectionViewModel>().Activities.Add(new ActivityView(Guid.NewGuid(), "OneDrive", ActivityState.Waiting, 1000, null, null, null, started, started, 27, true));

        var row = activity.DaemonActivities.ShouldHaveSingleItem();
        row.Title.ShouldBe("Waiting for the provider: OneDrive");
        row.ProgressText.ShouldBe("1,000 items indexed");
        row.HasPercent.ShouldBeFalse();
        row.EtaText.ShouldBeNull();
        row.RetryText.ShouldBe("Retrying in 27 seconds");
        row.ActionRequiredText.ShouldNotBeNull().ShouldNotStartWith("!");
    }

    [Fact]
    public void hide_the_daemon_section_when_nothing_is_reported()
    {
        using var host = OperationScreensHost.Create();

        host.Screen<ActivityViewModel>().HasDaemonActivities.ShouldBeFalse();
    }

    private static readonly DateTimeOffset Queued = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static QueuedOperationView Operation(string status, string type, string path, DateTimeOffset queued) => new(Guid.NewGuid(), "OneDrive", type, status, path, 2048, queued);

    [Fact]
    public void list_the_daemons_pending_and_running_operations_newest_first()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();
        var older = Operation("Running", "DownloadFile", "Docs/report.docx", Queued.AddMinutes(-5));
        var newer = Operation("Pending", "UploadFile", "Photos/a.jpg", Queued);

        host.Services.GetRequiredService<ConnectionViewModel>().Queue = new QueueView(1, 1, 0, 0, [older, newer]);

        activity.Rows.Select(row => row.Entry).ShouldBe([new OperationEntry(newer.OperationId.ToString(), "OneDrive", OperationType.Upload, OperationStatus.Pending, Queued, "Photos/a.jpg", 2048), new OperationEntry(older.OperationId.ToString(), "OneDrive", OperationType.Download, OperationStatus.Running, Queued.AddMinutes(-5), "Docs/report.docx", 2048)]);
        activity.Rows[0].Date.ShouldBe("20/11/2026");
        activity.Rows[0].Time.ShouldBe("12:00");
        activity.HasNoRows.ShouldBeFalse();
    }

    [Fact]
    public void follow_the_queue_and_say_so_when_it_empties()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();
        var connection = host.Services.GetRequiredService<ConnectionViewModel>();
        connection.Queue = new QueueView(1, 0, 0, 0, [Operation("Pending", "UploadFile", "a.txt", Queued)]);

        connection.Queue = new QueueView(0, 0, 1, 0, []);

        activity.Rows.ShouldBeEmpty();
        activity.HasNoRows.ShouldBeTrue();
        activity.NoMatchesText.ShouldBe("No operations to show yet.");
    }

    [Fact]
    public void filter_the_daemons_operations_by_type_and_path()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();
        host.Services.GetRequiredService<ConnectionViewModel>().Queue = new QueueView(3, 0, 0, 0, [Operation("Pending", "UploadFile", "a/one.txt", Queued), Operation("Pending", "DeleteLocal", "a/two.txt", Queued), Operation("Pending", "UploadFile", "b/three.txt", Queued)]);

        activity.SelectedType = activity.TypeChoices.Single(choice => choice.Value == nameof(OperationType.Upload));
        activity.PathText = "A/";

        activity.Rows.Select(row => row.Entry.Path).ShouldBe(["a/one.txt"]);
    }

    [Fact]
    public void open_the_operation_named_by_a_navigation_target()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();
        var target = Operation("Running", "RenameLocal", "x.txt", Queued);
        host.Services.GetRequiredService<ConnectionViewModel>().Queue = new QueueView(0, 1, 0, 0, [target]);

        activity.OnNavigatedTo(target.OperationId.ToString());

        activity.Details.Row!.Entry.Path.ShouldBe("x.txt");
    }

    [Fact]
    public void localise_its_export_explanation()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();

        activity.ExportDisabledReason.ShouldNotStartWith("!");
        activity.ExportText.ShouldNotStartWith("!");
        activity.Title.ShouldNotStartWith("!");
    }
}
