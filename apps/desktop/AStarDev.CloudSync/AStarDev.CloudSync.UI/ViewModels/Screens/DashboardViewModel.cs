using System.Windows.Input;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The trust dashboard of doc 17: health, progress, providers, queue, attention, activity and events, in that order. Values come from an <see cref="IDashboardSource"/>;
/// with none given it shows <see cref="PlaceholderDashboardSource"/> and labels everything as sample data. The conflict count is the exception: given a connection it is the live number of conflicts waiting for the user.
/// </summary>
internal sealed class DashboardViewModel : ViewModelBase, IScreen
{
    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<INavigationService> navigation;
    private readonly ConnectionViewModel? connection;
    private readonly DashboardSnapshot snapshot;

    /// <summary>Creates the dashboard and, when a connection is given, follows its conflicts.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats numbers and times.</param>
    /// <param name="navigation">Resolves the navigation service on use; a direct dependency would be a DI cycle.</param>
    /// <param name="source">The values to show; <see langword="null"/> means the placeholder sample values.</param>
    /// <param name="connection">Where the live conflict count comes from; <see langword="null"/> means the count of <paramref name="source"/>.</param>
    public DashboardViewModel(ILocalisationService localisation, ICultureService culture, Func<INavigationService> navigation, IDashboardSource? source = null, ConnectionViewModel? connection = null)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.navigation = navigation;
        this.connection = connection;
        snapshot = (source ?? new PlaceholderDashboardSource()).GetSnapshot();
        if (connection is not null) connection.Conflicts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ConflictsText));
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Dashboard;

    /// <inheritdoc/>
    public string Title => localisation.Get("Dashboard.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Dashboard.Glyph");

    /// <summary>Says the values shown are sample data, not real sync state.</summary>
    public string SampleDataNotice => localisation.Get("Dashboard.SampleData");

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

    /// <summary>Items analysed out of the total, for example "183,442 / 702,144 items analysed".</summary>
    public string ItemsAnalysedText => localisation.Format("Dashboard.Progress.ItemsAnalysed", ("done", snapshot.ItemsAnalysed), ("total", snapshot.ItemsTotal));

    /// <summary>Share of items analysed, 0 to 100; 0 while the total is unknown (zero).</summary>
    public double ProgressPercent => snapshot.ItemsTotal == 0 ? 0 : Math.Min(100, 100d * snapshot.ItemsAnalysed / snapshot.ItemsTotal);

    /// <summary>The pipeline stage running now, in words.</summary>
    public string PhaseText => localisation.Format("Dashboard.Progress.Phase", ("phase", localisation.Get($"Dashboard.Stage.{snapshot.Stage}")));

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
    public string ConflictsText => Count("Dashboard.Attention.Conflicts", connection?.Conflicts.Count ?? snapshot.Conflicts);

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

    /// <summary>Opens the Conflicts screen.</summary>
    public ICommand OpenConflictsCommand => new RelayCommand(() => navigation().Navigate(ScreenId.Conflicts));

    /// <summary>Opens the Queue screen.</summary>
    public ICommand OpenQueueCommand => new RelayCommand(() => navigation().Navigate(ScreenId.Queue));

    /// <summary>Opens the Providers screen.</summary>
    public ICommand OpenProvidersCommand => new RelayCommand(() => navigation().Navigate(ScreenId.Providers));

    /// <summary>Opens the Activity screen, where recent events are listed in full.</summary>
    public ICommand OpenActivityCommand => new RelayCommand(() => navigation().Navigate(ScreenId.Activity));

    private string Time(string key, DateTimeOffset value) => localisation.Format(key, ("time", culture.FormatTime(value)));

    private string Count(string key, long value) => localisation.Format(key, ("count", value));

    private StatusIndicatorViewModel ProviderRow((string Name, SyncHealth Health) provider)
    {
        var level = SyncHealthPresentation.LevelOf(provider.Health);

        return new StatusIndicatorViewModel(level, localisation.Format("Dashboard.Providers.Row", ("name", provider.Name), ("health", localisation.Get($"Common.Trust.{level}"))));
    }

    private PipelineStageRow StageRow(ReconciliationStage stage)
    {
        var state = stage.CompareTo(snapshot.Stage) switch
        {
            < 0 => "Done",
            0 => "Running",
            _ => "Pending"
        };

        return new PipelineStageRow(localisation.Get($"Dashboard.Stage.{stage}"), localisation.Get($"Dashboard.StageState.{state}"), stage == snapshot.Stage);
    }
}
