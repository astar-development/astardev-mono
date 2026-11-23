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
