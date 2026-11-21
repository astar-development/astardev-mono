using AStarDev.CloudSync.UI.Conflicts;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ConflictsViewModelShould
{
    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();

        return builder.Build();
    }

    private static ConflictsViewModel Resolve(IHost host) => host.Services.GetServices<IScreen>().OfType<ConflictsViewModel>().Single();

    [Fact]
    public void list_the_sample_conflicts_and_label_them_as_sample_data()
    {
        using var host = CreateHost();

        var conflicts = Resolve(host);

        conflicts.Rows.Count.ShouldBe(3);
        conflicts.SampleDataNotice.ShouldNotBeNullOrWhiteSpace();
        conflicts.SampleDataNotice.ShouldNotStartWith("!");
        conflicts.IsSampleData.ShouldBeTrue();
    }

    [Fact]
    public void show_status_as_colour_icon_and_text_with_age()
    {
        using var host = CreateHost();

        foreach (var row in Resolve(host).Rows)
        {
            row.Status.Text.ShouldNotStartWith("!");
            row.Status.IconKey.ShouldStartWith("Icon.Trust.");
            row.Status.ColourKey.ShouldStartWith("Trust.");
            row.AgeText.ShouldNotStartWith("!");
            row.ReasonText.ShouldNotStartWith("!");
        }
    }

    [Fact]
    public void offer_the_s21_resolution_actions()
    {
        using var host = CreateHost();

        var conflicts = Resolve(host);

        conflicts.Actions.Select(action => action.Resolution).ShouldBe([ConflictResolution.KeepLocal, ConflictResolution.KeepRemote, ConflictResolution.KeepBoth, ConflictResolution.Defer]);
        conflicts.Actions.ShouldAllBe(action => !action.IsEnabled);
        conflicts.Actions.ShouldAllBe(action => !action.Label.StartsWith("!"));
        conflicts.ActionsUnavailableText.ShouldNotStartWith("!");
        conflicts.RecoveryText.ShouldNotStartWith("!");
    }

    [Fact]
    public void require_confirmation_before_resolving()
    {
        using var host = CreateHost();
        var conflicts = Resolve(host);
        conflicts.Rows[0].IsSelected = true;

        conflicts.RequestResolution(ConflictResolution.KeepBoth);

        conflicts.IsConfirming.ShouldBeTrue();
        conflicts.PendingResolution.ShouldBe(ConflictResolution.KeepBoth);
        conflicts.ConfirmationText.ShouldNotStartWith("!");
        conflicts.CancelResolution();
        conflicts.IsConfirming.ShouldBeFalse();
    }

    [Fact]
    public void ignore_a_resolution_request_when_nothing_is_selected()
    {
        using var host = CreateHost();
        var conflicts = Resolve(host);

        conflicts.RequestResolution(ConflictResolution.KeepLocal);

        conflicts.IsConfirming.ShouldBeFalse();
    }

    [Fact]
    public void allow_bulk_selection()
    {
        using var host = CreateHost();
        var conflicts = Resolve(host);

        conflicts.SelectAll();
        conflicts.SelectedCount.ShouldBe(3);
        conflicts.HasSelection.ShouldBeTrue();

        conflicts.ClearSelection();
        conflicts.SelectedCount.ShouldBe(0);
    }

    [Fact]
    public void show_the_detail_of_the_conflict_a_deep_link_names()
    {
        using var host = CreateHost();
        var conflicts = Resolve(host);

        conflicts.OnNavigatedTo("sample-2");

        conflicts.SelectedConflict!.Item.Id.ShouldBe("sample-2");
        conflicts.SelectedConflict.LocalSizeText.ShouldNotStartWith("!");
    }

    [Fact]
    public void leave_the_selection_alone_for_an_unknown_deep_link_target()
    {
        using var host = CreateHost();
        var conflicts = Resolve(host);

        conflicts.OnNavigatedTo("nope");

        conflicts.SelectedConflict.ShouldBeNull();
    }
}
