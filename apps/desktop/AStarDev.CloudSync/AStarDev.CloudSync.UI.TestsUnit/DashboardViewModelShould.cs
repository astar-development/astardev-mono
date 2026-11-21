using System.Windows.Input;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class DashboardViewModelShould
{
    private sealed class FixedSource(DashboardSnapshot snapshot) : IDashboardSource
    {
        public DashboardSnapshot GetSnapshot() => snapshot;
    }

    private static readonly DateTimeOffset Moment = new(2026, 11, 21, 14, 32, 11, TimeSpan.Zero);

    private static DashboardSnapshot Snapshot(SyncHealth health, int conflicts = 0, int errors = 0, int warnings = 0) => new(
        health, Moment, Moment, Moment, 183442, 702144, ReconciliationStage.EnumerateRemoteState, 4232, TimeSpan.FromMinutes(17),
        [("OneDrive", SyncHealth.Healthy)], 1245, 8, 181223, 0, conflicts, errors, warnings, [(DashboardOperation.Download, "Documents/Design.md")], [DashboardEvent.QueueDrained]);

    private static LocalisationService RealLocalisation(CultureService culture)
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(culture, new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    private static (DashboardViewModel Dashboard, NavigationService Navigation) Create(DashboardSnapshot? snapshot = null)
    {
        var culture = new CultureService("en-GB");
        var navigation = new NavigationService(Enum.GetValues<ScreenId>().Select(id => new FakeScreen(id)).ToArray());

        return (new DashboardViewModel(RealLocalisation(culture), culture, () => navigation, new FixedSource(snapshot ?? Snapshot(SyncHealth.Active))), navigation);
    }

    private static IEnumerable<string> AllTexts(DashboardViewModel dashboard) =>
    [
        dashboard.Title, dashboard.Glyph, dashboard.SampleDataNotice, dashboard.Health.Headline, dashboard.Health.Summary, dashboard.Health.Indicator.Text,
        dashboard.LastReconciliationText, dashboard.LastProviderPollText, dashboard.LastQueueActivityText,
        dashboard.ProgressHeading, dashboard.ItemsAnalysedText, dashboard.PhaseText, dashboard.ThroughputText, dashboard.EstimatedRemainingText,
        dashboard.ProvidersHeading, dashboard.QueueHeading, dashboard.PendingText, dashboard.RunningText, dashboard.CompletedText, dashboard.FailedText,
        dashboard.AttentionHeading, dashboard.ConflictsText, dashboard.ErrorsText, dashboard.WarningsText, dashboard.ActivityHeading, dashboard.EventsHeading,
        ..dashboard.Stages.SelectMany(stage => new[] { stage.Name, stage.State }), ..dashboard.Providers.Select(provider => provider.Text), ..dashboard.Activities, ..dashboard.Events
    ];

    [Fact]
    public void always_show_attention_required()
    {
        var (dashboard, _) = Create(Snapshot(SyncHealth.Healthy));

        dashboard.ConflictsText.ShouldContain("0");
        dashboard.ErrorsText.ShouldContain("0");
        dashboard.WarningsText.ShouldContain("0");
        dashboard.AttentionHeading.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void never_show_a_bare_synchronising_state()
    {
        var (dashboard, _) = Create(Snapshot(SyncHealth.Active));

        dashboard.ItemsAnalysedText.ShouldContain("183,442");
        dashboard.PhaseText.ShouldNotBeNullOrWhiteSpace();
        dashboard.ThroughputText.ShouldContain("4,232");
        dashboard.EstimatedRemainingText.ShouldContain("17");
        dashboard.Health.Headline.ShouldNotBe("Synchronising");
    }

    [Fact]
    public void list_the_six_pipeline_stages_marking_the_current_one()
    {
        var (dashboard, _) = Create();

        dashboard.Stages.Count.ShouldBe(6);
        dashboard.Stages.Select(stage => stage.State).Distinct().Count().ShouldBe(3);
        dashboard.Stages.Single(stage => stage.IsCurrent).Name.ShouldBe(dashboard.Stages[1].Name);
    }

    [Fact]
    public void label_every_text_as_sample_data_with_no_missing_localisation()
    {
        var (dashboard, _) = Create();

        AllTexts(dashboard).ShouldAllBe(text => !string.IsNullOrWhiteSpace(text) && !text.StartsWith("!", StringComparison.Ordinal));
        dashboard.SampleDataNotice.ShouldContain("ample");
    }

    [Fact]
    public void use_the_placeholder_source_when_none_is_given()
    {
        var culture = new CultureService("en-GB");

        var dashboard = new DashboardViewModel(RealLocalisation(culture), culture, () => new NavigationService([new FakeScreen(ScreenId.Dashboard)]));

        dashboard.ConflictsText.ShouldContain("1");
        dashboard.Id.ShouldBe(ScreenId.Dashboard);
    }

    [Fact]
    public void deep_link_the_conflict_count_to_conflicts()
    {
        var (dashboard, navigation) = Create();

        dashboard.OpenConflictsCommand.Execute(null);

        navigation.Current.Id.ShouldBe(ScreenId.Conflicts);
    }

    [Fact]
    public void deep_link_the_queue_providers_and_recent_events()
    {
        var (dashboard, navigation) = Create();
        var cases = new (ICommand Command, ScreenId Expected)[]
        {
            (dashboard.OpenQueueCommand, ScreenId.Queue),
            (dashboard.OpenProvidersCommand, ScreenId.Providers),
            (dashboard.OpenActivityCommand, ScreenId.Activity)
        };

        foreach (var (command, expected) in cases)
        {
            command.Execute(null);
            navigation.Current.Id.ShouldBe(expected);
        }
    }
}
