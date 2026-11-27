using System.Windows.Input;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The trust dashboard of doc 17: health, progress, providers, queue, attention, activity and events, in that order. Values come from an <see cref="IDashboardSource"/>: read when the daemon link comes up, then replaced by each <c>DashboardChanged</c> event.
/// While the link is down, or before the first read, it shows <see cref="DashboardSnapshot.Disconnected"/> and <see cref="DisconnectedNotice"/>, never the last values. The conflict count is the live number of conflicts the connection holds.
/// </summary>
internal sealed class DashboardViewModel : ViewModelBase, IScreen, IDisposable
{
    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<INavigationService> navigation;
    private readonly IDashboardSource source;
    private readonly ConnectionViewModel connection;
    private readonly CancellationTokenSource lifetime = new();
    private Option<DashboardSnapshot> shown = Option.None<DashboardSnapshot>();

    /// <summary>Creates the dashboard and starts following the daemon link, its conflicts and its pushed changes.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats numbers and times.</param>
    /// <param name="navigation">Resolves the navigation service on use; a direct dependency would be a DI cycle.</param>
    /// <param name="source">Where the snapshot is read from each time the link comes up.</param>
    /// <param name="connection">The daemon link: its state decides whether values are shown, its pushed events change them, and its conflicts give the conflict count.</param>
    public DashboardViewModel(ILocalisationService localisation, ICultureService culture, Func<INavigationService> navigation, IDashboardSource source, ConnectionViewModel connection)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.navigation = navigation;
        this.source = source;
        this.connection = connection;
        connection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectionViewModel.State)) OnLinkChanged();
            if (args.PropertyName == nameof(ConnectionViewModel.ConflictsTotal)) OnPropertyChanged(nameof(ConflictsText));
        };
        connection.EventReceived += OnEvent;
        OnLinkChanged();
    }

    private DashboardSnapshot snapshot => shown.Value is Some<DashboardSnapshot> some ? some.Value : DashboardSnapshot.Disconnected;

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Dashboard;

    /// <inheritdoc/>
    public string Title => localisation.Get("Dashboard.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Dashboard.Glyph");

    /// <summary>Says the daemon is not connected, so the values are not current.</summary>
    public string DisconnectedNotice => localisation.Get("Dashboard.Disconnected");

    /// <summary>Whether the daemon values are missing: the link is down, or its first snapshot has not arrived. The view shows <see cref="DisconnectedNotice"/> then.</summary>
    public bool IsDisconnected => shown.Value is not Some<DashboardSnapshot>;

    /// <summary>Whether sync cannot run yet because no account is connected or no folder is selected. The view then shows <see cref="SetupNotice"/> in place of the sync progress.</summary>
    public bool IsSetupNeeded => !IsDisconnected && snapshot.Setup != IPC.DashboardSetup.Ready;

    /// <summary>Whether the sync progress is meaningful, which is the opposite of <see cref="IsSetupNeeded"/>.</summary>
    public bool IsSetupComplete => !IsSetupNeeded;

    /// <summary>Tells the user what to do next: connect an account, or select folders to sync.</summary>
    public string SetupNotice => localisation.Get($"Dashboard.Setup.{(snapshot.Setup == IPC.DashboardSetup.NoAccount ? "NoAccount" : "NoFolders")}");

    /// <summary>Text of the button that opens the Providers screen, where accounts are connected and folders selected.</summary>
    public string SetupActionText => localisation.Get("Dashboard.Setup.Action");

    /// <summary>Headline, summary, colour and icon of the Trust Status card.</summary>
    public SyncHealthPresentation Health => SyncHealthPresentation.For(snapshot.Health, localisation);

    /// <summary>When the last reconciliation succeeded, as a labelled time.</summary>
    public string LastReconciliationText => Time("Dashboard.Trust.LastReconciliation", snapshot.LastReconciliation);

    /// <summary>When a provider was last polled, as a labelled time.</summary>
    public string LastProviderPollText => Time("Dashboard.Trust.LastProviderPoll", snapshot.LastProviderPoll);

    /// <summary>When the queue last did anything, as a labelled time.</summary>
    public string LastQueueActivityText => Time("Dashboard.Trust.LastQueueActivity", snapshot.LastQueueActivity);

    /// <summary>Heading of the Sync Progress card.</summary>
    public string ProgressHeading => localisation.Get("Dashboard.Progress.Heading");

    /// <summary>Items done out of the total: "183,442 / 702,144 items analysed" while indexing, "70 / 607 items synced" afterwards.</summary>
    public string ItemsAnalysedText => localisation.Format(snapshot.Stage.Value is Some<ReconciliationStage> { Value: ReconciliationStage.EnumerateRemoteState } ? "Dashboard.Progress.ItemsAnalysed" : "Dashboard.Progress.ItemsSynced", ("done", snapshot.ItemsAnalysed), ("total", snapshot.ItemsTotal));

    /// <summary>Share of items analysed, 0 to 100; 0 while the total is unknown (zero).</summary>
    public double ProgressPercent => snapshot.ItemsTotal == 0 ? 0 : Math.Min(100, 100d * snapshot.ItemsAnalysed / snapshot.ItemsTotal);

    /// <summary>The pipeline stage running now, in words.</summary>
    public string PhaseText => localisation.Format("Dashboard.Progress.Phase", ("phase", snapshot.Stage.Value is Some<ReconciliationStage> stage ? localisation.Get($"Dashboard.Stage.{stage.Value}") : localisation.Get("Dashboard.Stage.None")));

    /// <summary>Items per second.</summary>
    public string ThroughputText => localisation.Format("Dashboard.Progress.Throughput", ("rate", snapshot.ItemsPerSecond));

    /// <summary>Estimated time left in whole minutes.</summary>
    public string EstimatedRemainingText => localisation.Format("Dashboard.Progress.Remaining", ("minutes", (long)snapshot.EstimatedRemaining.TotalMinutes));

    /// <summary>The six pipeline stages in order, each marked done, running or pending.</summary>
    public IReadOnlyList<PipelineStageRow> Stages => Enum.GetValues<ReconciliationStage>().Select(StageRow).ToList();

    /// <summary>Heading of the Providers card.</summary>
    public string ProvidersHeading => localisation.Get("Dashboard.Providers.Heading");

    /// <summary>Each provider with its health as colour, icon and text.</summary>
    public IReadOnlyList<StatusIndicatorViewModel> Providers => snapshot.Providers.Select(ProviderRow).ToList();

    /// <summary>Heading of the Queue card.</summary>
    public string QueueHeading => localisation.Get("Dashboard.Queue.Heading");

    /// <summary>Operations waiting to start.</summary>
    public string PendingText => Count("Dashboard.Queue.Pending", snapshot.Pending);

    /// <summary>Operations executing now.</summary>
    public string RunningText => Count("Dashboard.Queue.Running", snapshot.Running);

    /// <summary>Operations finished.</summary>
    public string CompletedText => Count("Dashboard.Queue.Completed", snapshot.Completed);

    /// <summary>Operations that failed.</summary>
    public string FailedText => Count("Dashboard.Queue.Failed", snapshot.Failed);

    /// <summary>Heading of the Attention Required card, which is always shown, even at zero.</summary>
    public string AttentionHeading => localisation.Get("Dashboard.Attention.Heading");

    /// <summary>Conflict count, shown even when zero: the conflicts waiting for the user, deferred ones included, kept current as the daemon reports them.</summary>
    public string ConflictsText => Count("Dashboard.Attention.Conflicts", connection.ConflictsTotal);

    /// <summary>Error count, shown even when zero.</summary>
    public string ErrorsText => Count("Dashboard.Attention.Errors", snapshot.Errors);

    /// <summary>Warning count, shown even when zero.</summary>
    public string WarningsText => Count("Dashboard.Attention.Warnings", snapshot.Warnings);

    /// <summary>Heading of the Current Activity card.</summary>
    public string ActivityHeading => localisation.Get("Dashboard.Activity.Heading");

    /// <summary>The operations running now, each as "Download: path".</summary>
    public IReadOnlyList<string> Activities => snapshot.Activities.Select(activity => localisation.Format("Dashboard.Activity.Row", ("operation", localisation.Get($"Dashboard.Operation.{activity.Operation}")), ("path", activity.Path))).ToList();

    /// <summary>Heading of the Recent Events card.</summary>
    public string EventsHeading => localisation.Get("Dashboard.Events.Heading");

    /// <summary>Recent events in words, newest first.</summary>
    public IReadOnlyList<string> Events => snapshot.Events.Select(item => localisation.Get($"Dashboard.Event.{item}")).ToList();

    /// <summary>Text of the button that opens the Conflicts screen.</summary>
    public string OpenConflictsText => localisation.Get("Dashboard.Open.Conflicts");

    /// <summary>Text of the button that opens the Queue screen.</summary>
    public string OpenQueueText => localisation.Get("Dashboard.Open.Queue");

    /// <summary>Text of the button that opens the Providers screen.</summary>
    public string OpenProvidersText => localisation.Get("Dashboard.Open.Providers");

    /// <summary>Text of the button that opens the Activity screen.</summary>
    public string OpenActivityText => localisation.Get("Dashboard.Open.Activity");

    /// <summary>Opens the Providers screen from the setup notice.</summary>
    public ICommand OpenSetupCommand => OpenProvidersCommand;

    /// <summary>Opens the Conflicts screen.</summary>
    public ICommand OpenConflictsCommand => new RelayCommand(() => navigation().Navigate(ScreenId.Conflicts));

    /// <summary>Opens the Queue screen.</summary>
    public ICommand OpenQueueCommand => new RelayCommand(() => navigation().Navigate(ScreenId.Queue));

    /// <summary>Opens the Providers screen.</summary>
    public ICommand OpenProvidersCommand => new RelayCommand(() => navigation().Navigate(ScreenId.Providers));

    /// <summary>Opens the Activity screen, where recent events are listed in full.</summary>
    public ICommand OpenActivityCommand => new RelayCommand(() => navigation().Navigate(ScreenId.Activity));

    /// <inheritdoc/>
    public void Dispose() => lifetime.Dispose();

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (await source.GetSnapshotAsync(cancellationToken) is Ok<DashboardSnapshot> read && shown.Value is not Some<DashboardSnapshot>) Show(read.Value);
    }

    private void OnLinkChanged()
    {
        if (connection.State == ConnectionState.Connected) _ = RefreshAsync(lifetime.Token);
        else Show(Option.None<DashboardSnapshot>());
    }

    private void OnEvent(IPC.IEvent received)
    {
        if (received is IPC.DashboardChanged changed && connection.State == ConnectionState.Connected) Show(DashboardSnapshot.From(changed.Dashboard));
    }

    private void Show(DashboardSnapshot next) => Show(Option.Some(next));

    private void Show(Option<DashboardSnapshot> next)
    {
        shown = next;
        OnPropertyChanged(string.Empty);
    }

    private string Time(string key, Option<DateTimeOffset> value) => localisation.Format(key, ("time", value.Value is Some<DateTimeOffset> at ? culture.FormatTime(at.Value.ToLocalTime()) : localisation.Get("Dashboard.Time.None")));

    private string Count(string key, long value) => localisation.Format(key, ("count", value));

    private StatusIndicatorViewModel ProviderRow((string Name, SyncHealth Health) provider)
    {
        var level = SyncHealthPresentation.LevelOf(provider.Health);

        return new StatusIndicatorViewModel(level, localisation.Format("Dashboard.Providers.Row", ("name", provider.Name), ("health", localisation.Get($"Common.Trust.{level}"))));
    }

    private bool IsDrained(ReconciliationStage stage) => stage == ReconciliationStage.ExecuteQueue && snapshot.Pending == 0 && snapshot.Running == 0;

    private PipelineStageRow StageRow(ReconciliationStage stage)
    {
        var current = snapshot.Stage.Value is Some<ReconciliationStage> some ? (ReconciliationStage?)some.Value : null;
        var state = current is null ? "Pending" : stage.CompareTo(current.Value) switch
        {
            < 0 => "Done",
            0 => IsDrained(stage) ? "Done" : "Running",
            _ => "Pending"
        };

        return new PipelineStageRow(localisation.Get($"Dashboard.Stage.{stage}"), localisation.Get($"Dashboard.StageState.{state}"), stage == current);
    }
}
