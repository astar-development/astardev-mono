using System.Windows.Input;
using AStarDev.CloudSync.IPC;
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
        public Task<Result<DashboardSnapshot, IpcError>> GetSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult<Result<DashboardSnapshot, IpcError>>(new Ok<DashboardSnapshot>(snapshot));
    }

    private static readonly DateTimeOffset Moment = new(2026, 11, 21, 14, 32, 11, TimeSpan.Zero);

    private static DashboardSnapshot Snapshot(SyncHealth health, int conflicts = 0, int errors = 0, int warnings = 0, Option<ReconciliationStage>? stage = null, IPC.DashboardSetup setup = IPC.DashboardSetup.Ready) => new(
        health, Option.Some(Moment), Option.Some(Moment), Option.Some(Moment), 183442, 702144, stage ?? Option.Some(ReconciliationStage.EnumerateRemoteState), 4232, TimeSpan.FromMinutes(17),
        [("OneDrive", SyncHealth.Healthy)], 1245, 8, 181223, 0, conflicts, errors, warnings, [(DashboardOperation.Download, "Documents/Design.md")], [DashboardEvent.QueueDrained], setup);

    private static LocalisationService RealLocalisation(CultureService culture)
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(culture, new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    private static ConnectionViewModel Connected(FakeIpcClient? client = null)
        => new(() => client ?? new FakeIpcClient([], available: false), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)) { State = ConnectionState.Connected };

    private static (DashboardViewModel Dashboard, NavigationService Navigation) Create(DashboardSnapshot? snapshot = null)
    {
        var culture = new CultureService("en-GB");
        var navigation = new NavigationService(Enum.GetValues<ScreenId>().Select(id => new FakeScreen(id)).ToArray());

        return (new DashboardViewModel(RealLocalisation(culture), culture, () => navigation, new FixedSource(snapshot ?? Snapshot(SyncHealth.Active)), Connected()), navigation);
    }

    private static IEnumerable<string> AllTexts(DashboardViewModel dashboard) =>
    [
        dashboard.Title, dashboard.Glyph, dashboard.DisconnectedNotice, dashboard.Health.Headline, dashboard.Health.Summary, dashboard.Health.Indicator.Text,
        dashboard.LastReconciliationText, dashboard.LastProviderPollText, dashboard.LastQueueActivityText,
        dashboard.ProgressHeading, dashboard.ItemsAnalysedText, dashboard.PhaseText, dashboard.ThroughputText, dashboard.EstimatedRemainingText,
        dashboard.ProvidersHeading, dashboard.QueueHeading, dashboard.PendingText, dashboard.RunningText, dashboard.CompletedText, dashboard.FailedText,
        dashboard.AttentionHeading, dashboard.ConflictsText, dashboard.ErrorsText, dashboard.WarningsText, dashboard.ActivityHeading, dashboard.EventsHeading,
        ..dashboard.Stages.SelectMany(stage => new[] { stage.Name, stage.State }), ..dashboard.Providers.Select(provider => provider.Text), ..dashboard.Activities, ..dashboard.Events
    ];

    [Fact]
    public void report_progress_as_a_percentage_of_items_analysed()
    {
        var (dashboard, _) = Create();

        dashboard.ProgressPercent.ShouldBe(100d * 183442 / 702144);
    }

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
    public void call_the_progress_items_analysed_while_indexing_and_synced_afterwards()
    {
        var (indexing, _) = Create(Snapshot(SyncHealth.Active, stage: Option.Some(ReconciliationStage.EnumerateRemoteState)));
        var (transferring, _) = Create(Snapshot(SyncHealth.Active, stage: Option.Some(ReconciliationStage.ExecuteQueue)));

        indexing.ItemsAnalysedText.ShouldContain("items analysed");
        transferring.ItemsAnalysedText.ShouldContain("items synced");
    }

    [Fact]
    public void show_every_stage_as_pending_and_none_as_current_before_any_sync_has_run()
    {
        var (dashboard, _) = Create(Snapshot(SyncHealth.Active, stage: Option.None<ReconciliationStage>()));

        dashboard.Stages.Select(stage => stage.State).Distinct().ShouldBe(["Pending"]);
        dashboard.Stages.ShouldAllBe(stage => !stage.IsCurrent);
        dashboard.PhaseText.ShouldContain("Not started");
    }

    [Theory]
    [InlineData(IPC.DashboardSetup.NoAccount, "No account is connected")]
    [InlineData(IPC.DashboardSetup.NoFolders, "No folders are selected")]
    public void ask_for_setup_instead_of_showing_sync_progress(IPC.DashboardSetup setup, string notice)
    {
        var (dashboard, _) = Create(Snapshot(SyncHealth.SetupRequired, setup: setup));

        dashboard.IsSetupNeeded.ShouldBeTrue();
        dashboard.IsSetupComplete.ShouldBeFalse();
        dashboard.SetupNotice.ShouldContain(notice);
        dashboard.Health.Headline.ShouldBe("SET UP SYNC");
    }

    [Fact]
    public void not_ask_for_setup_once_an_account_and_folders_exist()
    {
        var (dashboard, _) = Create();

        dashboard.IsSetupNeeded.ShouldBeFalse();
        dashboard.IsSetupComplete.ShouldBeTrue();
    }

    [Fact]
    public void open_providers_from_the_setup_notice()
    {
        var (dashboard, navigation) = Create(Snapshot(SyncHealth.SetupRequired, setup: IPC.DashboardSetup.NoAccount));

        dashboard.OpenSetupCommand.Execute(null);

        navigation.Current.Id.ShouldBe(ScreenId.Providers);
    }

    [Fact]
    public void have_no_missing_localisation()
    {
        var (dashboard, _) = Create();

        AllTexts(dashboard).ShouldAllBe(text => !string.IsNullOrWhiteSpace(text) && !text.StartsWith("!", StringComparison.Ordinal));
    }

    [Fact]
    public void deep_link_the_conflict_count_to_conflicts()
    {
        var (dashboard, navigation) = Create();

        dashboard.OpenConflictsCommand.Execute(null);

        navigation.Current.Id.ShouldBe(ScreenId.Conflicts);
    }

    [Fact]
    public void show_the_live_conflict_count_and_follow_it_as_conflicts_come_and_go()
    {
        var culture = new CultureService("en-GB");
        var navigation = new NavigationService([new FakeScreen(ScreenId.Dashboard)]);
        var connection = Connected();
        var dashboard = new DashboardViewModel(RealLocalisation(culture), culture, () => navigation, new FixedSource(Snapshot(SyncHealth.Active, conflicts: 7)), connection);
        var changed = new List<string?>();
        dashboard.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        dashboard.ConflictsText.ShouldBe("Conflicts : 0");

        var conflict = new IPC.ConflictView(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ModifyModify", Moment, "Open", "a.txt", null, null);
        connection.Conflicts.Add(conflict);
        connection.Conflicts.Add(conflict with { ConflictId = Guid.NewGuid(), Status = "Deferred" });

        dashboard.ConflictsText.ShouldBe("Conflicts : 2");
        changed.ShouldContain(nameof(DashboardViewModel.ConflictsText));

        connection.Conflicts.RemoveAt(0);

        dashboard.ConflictsText.ShouldBe("Conflicts : 1");
    }

    [Fact]
    public void word_the_conflicts_link_as_review_conflicts()
    {
        var (dashboard, _) = Create();

        dashboard.OpenConflictsText.ShouldBe("Review Conflicts");
    }

    [Fact]
    public void deep_link_the_queue_providers_and_recent_events()
    {
        var (dashboard, navigation) = Create();
        var cases = new (System.Windows.Input.ICommand Command, ScreenId Expected)[]
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

    [Fact]
    public void show_the_snapshot_from_the_daemon()
    {
        var (dashboard, _) = Create();

        dashboard.IsDisconnected.ShouldBeFalse();
        dashboard.ItemsAnalysedText.ShouldContain("183,442");
        dashboard.Providers.Select(provider => provider.Text).ShouldBe(["OneDrive - Healthy"]);
    }

    [Fact]
    public async Task apply_pushed_changes()
    {
        var client = new FakeIpcClient([], available: true);
        var connection = new ConnectionViewModel(() => client, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        var culture = new CultureService("en-GB");
        using var dashboard = new DashboardViewModel(RealLocalisation(culture), culture, () => new NavigationService([new FakeScreen(ScreenId.Dashboard)]), new FixedSource(Snapshot(SyncHealth.Active)), connection);
        using var source = new CancellationTokenSource();
        var run = connection.RunAsync(source.Token);
        await WaitForAsync(() => connection.State == ConnectionState.Connected);

        client.Raise(new DashboardChanged(new DashboardView(ProviderHealthState.Healthy, DashboardStage.ExecuteQueue, null, null, null, 5, 10, null, null, [], 3, 0, 0, 0, 0, 0, 0, [new DashboardOperationView("DownloadFile", "a.txt")], ["QueueDrained"], IPC.DashboardSetup.Ready)));

        dashboard.PendingText.ShouldBe("Pending : 3");
        dashboard.ItemsAnalysedText.ShouldContain("5 / 10");
        dashboard.Activities.ShouldBe(["Download: a.txt"]);
        dashboard.Events.ShouldBe(["Queue drained"]);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public void show_disconnected_when_the_daemon_link_is_down()
    {
        var culture = new CultureService("en-GB");
        var connection = Connected();
        using var dashboard = new DashboardViewModel(RealLocalisation(culture), culture, () => new NavigationService([new FakeScreen(ScreenId.Dashboard)]), new FixedSource(Snapshot(SyncHealth.Active)), connection);
        dashboard.IsDisconnected.ShouldBeFalse();

        connection.State = ConnectionState.Disconnected;

        dashboard.IsDisconnected.ShouldBeTrue();
        dashboard.Health.Headline.ShouldBe("SYNC HAS STOPPED");
        dashboard.ItemsAnalysedText.ShouldContain("0 / 0");
        dashboard.DisconnectedNotice.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task honour_cancellation_when_refreshing()
    {
        var culture = new CultureService("en-GB");
        using var dashboard = new DashboardViewModel(RealLocalisation(culture), culture, () => new NavigationService([new FakeScreen(ScreenId.Dashboard)]), new IpcDashboardSource(() => new FakeIpcClient([], available: true)), new ConnectionViewModel(() => new FakeIpcClient([], available: false), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => dashboard.RefreshAsync(cancelled.Token));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10, TestContext.Current.CancellationToken);
        condition().ShouldBeTrue();
    }
}
