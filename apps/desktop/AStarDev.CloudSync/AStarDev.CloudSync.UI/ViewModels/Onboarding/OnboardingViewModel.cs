using System.ComponentModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Onboarding;

/// <summary>
/// The first-run flow: Welcome, Connect Provider, Select Sync Scopes, Review Configuration and Initial Synchronisation, then the Dashboard.
/// It reuses <see cref="ConnectViewModel"/> and <see cref="ScopeSelectionViewModel"/> for the real work. Nothing is sent to the daemon that creates a sync job
/// until <see cref="ConfirmAsync"/> runs on the Review step. It shows itself only when the daemon holds no account.
/// </summary>
internal sealed partial class OnboardingViewModel : ViewModelBase
{
    private const int StepCount = 5;

    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<IIpcClient> client;
    private readonly ConnectionViewModel connection;
    private readonly Func<INavigationService> navigation;
    private readonly AccountsViewModel accounts;
    private readonly ConnectViewModel connect;
    private readonly FolderTreeViewModel folders;
    private readonly ScopeSelectionViewModel selection;
    private OnboardingStep step = OnboardingStep.Welcome;
    private bool isActive;
    private bool evaluated;
    private Option<DriveInfoView> driveInfo = Option.None<DriveInfoView>();

    /// <summary>Starts listening: the first time the daemon link comes up, <see cref="EvaluateFirstRunCommand"/> decides whether to show the flow.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every number.</param>
    /// <param name="client">Asks the daemon for the provider's storage figures.</param>
    /// <param name="navigation">Requested lazily, because the navigation service is built from the screens. Used to land on the Dashboard.</param>
    /// <param name="connection">The daemon link; a first connection triggers the first-run check, and its activities feed the synchronisation step.</param>
    /// <param name="accounts">Tells whether an account already exists, and names the connected provider on the review.</param>
    /// <param name="connect">Signs in to the provider.</param>
    /// <param name="folders">The remote folders, used to give the chosen scopes their names.</param>
    /// <param name="selection">The ticked folders and local folder; also creates the sync job on confirmation.</param>
    public OnboardingViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> client, Func<INavigationService> navigation, ConnectionViewModel connection, AccountsViewModel accounts, ConnectViewModel connect, FolderTreeViewModel folders, ScopeSelectionViewModel selection)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.client = client;
        this.connection = connection;
        this.navigation = navigation;
        this.accounts = accounts;
        this.connect = connect;
        this.folders = folders;
        this.selection = selection;
        connection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectionViewModel.State) && connection.State == ConnectionState.Connected) EvaluateFirstRunCommand.Execute(null);
        };
        connect.PropertyChanged += (_, _) => RaiseContinueChanged();
        connection.Activities.CollectionChanged += (_, _) => RaiseProgressChanged();
        selection.PropertyChanged += (_, args) => OnSelectionChanged(args);
        selection.SelectedFolderIds.CollectionChanged += (_, _) => OnSelectionChanged(null);
    }

    /// <summary>True while the flow covers the window. False until the first-run check finds no account, and again after <see cref="Skip"/> or <see cref="Finish"/>.</summary>
    public bool IsActive
    {
        get => isActive;
        private set => SetProperty(ref isActive, value);
    }

    /// <summary>The step being shown. Moves forward only through <see cref="Next"/> and <see cref="ConfirmAsync"/>.</summary>
    public OnboardingStep Step
    {
        get => step;
        private set
        {
            if (!SetProperty(ref step, value)) return;

            OnPropertyChanged(nameof(IsWelcomeStep));
            OnPropertyChanged(nameof(IsConnectStep));
            OnPropertyChanged(nameof(IsScopesStep));
            OnPropertyChanged(nameof(IsReviewStep));
            OnPropertyChanged(nameof(IsInitialSyncStep));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ReviewProvider));
            OnPropertyChanged(nameof(ReviewLocalPath));
            OnPropertyChanged(nameof(ReviewScopes));
            RaiseContinueChanged();
            if (step == OnboardingStep.Review) LoadDriveInfoCommand.Execute(null);
        }
    }

    /// <summary>True on <see cref="OnboardingStep.Welcome"/>.</summary>
    public bool IsWelcomeStep => step == OnboardingStep.Welcome;

    /// <summary>True on <see cref="OnboardingStep.ConnectProvider"/>.</summary>
    public bool IsConnectStep => step == OnboardingStep.ConnectProvider;

    /// <summary>True on <see cref="OnboardingStep.SelectScopes"/>.</summary>
    public bool IsScopesStep => step == OnboardingStep.SelectScopes;

    /// <summary>True on <see cref="OnboardingStep.Review"/>.</summary>
    public bool IsReviewStep => step == OnboardingStep.Review;

    /// <summary>True on <see cref="OnboardingStep.InitialSync"/>.</summary>
    public bool IsInitialSyncStep => step == OnboardingStep.InitialSync;

    /// <summary>Localised "Step n of 5" for the current step.</summary>
    public string ProgressText => localisation.Format("Onboarding.Progress", ("current", (int)step + 1), ("total", StepCount));

    /// <summary>The existing connect view model, bound by the Connect step.</summary>
    public ConnectViewModel Connect => connect;

    /// <summary>The remote folder tree, bound by the scope step.</summary>
    public FolderTreeViewModel Folders => folders;

    /// <summary>The ticked folders, local folder and job state, bound by the scope and review steps.</summary>
    public ScopeSelectionViewModel Selection => selection;

    /// <summary>True once a sign-in has succeeded in this session. The Connect step cannot be left before.</summary>
    public bool IsConnected => connect.ItemCount is not null;

    /// <summary>True when <see cref="Next"/> would move forward: Welcome always; Connect once connected; Scopes once a folder is ticked and a local folder given; never on Review or later.</summary>
    public bool CanContinue => step switch
    {
        OnboardingStep.Welcome => true,
        OnboardingStep.ConnectProvider => IsConnected,
        OnboardingStep.SelectScopes => selection.SelectedFolderIds.Count > 0 && !string.IsNullOrWhiteSpace(selection.LocalRoot),
        _ => false
    };

    /// <summary>True on the Connect, Scopes and Review steps. Welcome has nothing before it, and a started synchronisation cannot be taken back.</summary>
    public bool CanGoBack => step is OnboardingStep.ConnectProvider or OnboardingStep.SelectScopes or OnboardingStep.Review;

    /// <summary>The connected provider and account for the review, or a generic label before the account list has loaded.</summary>
    public string ReviewProvider => accounts.Accounts.FirstOrDefault() is { } account
        ? localisation.Format("Onboarding.Review.ProviderValue", ("account", account.DisplayName))
        : localisation.Get("Onboarding.Review.ProviderUnknown");

    /// <summary>The local folder the files will be stored in, as typed or picked.</summary>
    public string ReviewLocalPath => selection.LocalRoot.Trim();

    /// <summary>The names of the ticked folders. A folder whose name is not loaded in the tree shows its provider id.</summary>
    public IReadOnlyList<string> ReviewScopes => [.. selection.SelectedFolderIds.Select(NameOf)];

    /// <summary>The provider's estimate of the item count, or the localised word for unknown when the provider has none or has not answered.</summary>
    public string EstimatedItemsText => driveInfo.Bind(info => info.ItemCountEstimate is { } count ? Option.Some(culture.FormatNumber(count)) : Option.None<string>()).GetValueOrDefault(Unknown());

    /// <summary>The storage left and allowed by the provider, or the localised word for unknown until the provider has answered.</summary>
    public string AvailableStorageText => driveInfo.Match(
        info => localisation.Format("Onboarding.Review.AvailableStorageValue", ("remaining", Gigabytes(info.RemainingBytes)), ("total", Gigabytes(info.TotalBytes))),
        Unknown);

    /// <summary>The state of the initial synchronisation as the daemon reports it, or unknown before the first report.</summary>
    public string PhaseText => Activity.Match(report => localisation.Get($"Activity.Daemon.State.{report.State}"), Unknown);

    /// <summary>Items done, with the total when the daemon knows it, or unknown before the first report.</summary>
    public string ItemCountsText => Activity.Match(
        report => report.ItemsTotal is { } total
            ? localisation.Format("Onboarding.Sync.ItemsOfTotal", ("done", culture.FormatNumber(report.ItemsDone)), ("total", culture.FormatNumber(total)))
            : culture.FormatNumber(report.ItemsDone),
        Unknown);

    /// <summary>Items per second over the daemon's recent window, or unknown when the daemon has no rate.</summary>
    public string ThroughputText => Activity.Bind(report => Option.FromNullable(report.ItemsPerSecond)).Match(
        rate => localisation.Format("Onboarding.Sync.ThroughputValue", ("rate", rate.ToString("N1", culture.Culture))),
        Unknown);

    /// <summary>The daemon's estimate of the time left for the whole job in minutes, or unknown when the daemon has none.</summary>
    public string RemainingText => Activity.Bind(report => Option.FromNullable(report.EtaSeconds)).Match(
        seconds => seconds < 60 ? localisation.Get("Onboarding.Sync.RemainingSoon") : localisation.Format("Onboarding.Sync.RemainingMinutes", ("minutes", (long)Math.Round(seconds / 60d))),
        Unknown);

    /// <summary>
    /// Asks the daemon for the provider's storage figures and item estimate, shown on the Review step. Runs by itself on arriving at Review. A failed question leaves the values unknown.
    /// Cancellation surfaces as <see cref="OperationCanceledException"/>. Also exposed to the view as <c>LoadDriveInfoCommand</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancelling abandons the question.</param>
    [RelayCommand]
    public async Task LoadDriveInfoAsync(CancellationToken cancellationToken)
    {
        driveInfo = selection.AccountId is { } accountId && await client().QueryAsync(new GetDriveInfo(accountId), cancellationToken) is Ok<DriveInfoView> ok ? Option.Some(ok.Value) : Option.None<DriveInfoView>();
        OnPropertyChanged(nameof(EstimatedItemsText));
        OnPropertyChanged(nameof(AvailableStorageText));
    }

    /// <summary>
    /// Asks the daemon for its accounts once and shows the flow when there are none. A failed question (daemon unreachable) shows nothing and is asked again at the next
    /// connection. After a definite answer it never asks again, so a reconnect cannot interrupt a flow in progress. Cancellation surfaces as <see cref="OperationCanceledException"/>.
    /// Also exposed to the view as <c>EvaluateFirstRunCommand</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancelling abandons the question.</param>
    [RelayCommand]
    public async Task EvaluateFirstRunAsync(CancellationToken cancellationToken)
    {
        if (evaluated) return;

        await accounts.RefreshAsync(cancellationToken);
        if (accounts.ErrorMessage is not null) return;

        evaluated = true;
        if (accounts.Accounts.Count > 0) return;

        Step = OnboardingStep.Welcome;
        IsActive = true;
    }

    /// <summary>Moves to the next step when <see cref="CanContinue"/> allows it; otherwise does nothing. Also exposed to the view as <c>NextCommand</c>.</summary>
    [RelayCommand]
    public void Next()
    {
        if (CanContinue) Step = (OnboardingStep)((int)step + 1);
    }

    /// <summary>Moves to the previous step when <see cref="CanGoBack"/> allows it; otherwise does nothing. Also exposed to the view as <c>BackCommand</c>.</summary>
    [RelayCommand]
    public void Back()
    {
        if (CanGoBack) Step = (OnboardingStep)((int)step - 1);
    }

    /// <summary>
    /// The one place synchronisation is set up: on the Review step, creates the sync job with the chosen folders and moves to <see cref="OnboardingStep.InitialSync"/>.
    /// On any other step it does nothing. When the daemon refuses, the step stays Review and <see cref="ScopeSelectionViewModel.StatusMessage"/> says why.
    /// Cancellation surfaces as <see cref="OperationCanceledException"/>. Also exposed to the view as <c>ConfirmCommand</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancelling abandons the wait; the daemon may still finish the request.</param>
    [RelayCommand]
    public async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        if (step != OnboardingStep.Review) return;

        await selection.CreateJobAsync(cancellationToken);
        if (selection.HasJob) Step = OnboardingStep.InitialSync;
    }

    /// <summary>Closes the flow without creating anything, so the user can set CloudSync up later. Also exposed to the view as <c>SkipCommand</c>.</summary>
    [RelayCommand]
    public void Skip() => IsActive = false;

    /// <summary>Closes the flow and shows the Dashboard. Also exposed to the view as <c>FinishCommand</c>.</summary>
    [RelayCommand]
    public void Finish()
    {
        IsActive = false;
        _ = navigation().Navigate(ScreenId.Dashboard);
    }

    private Option<ActivityView> Activity => connection.Activities.Count > 0 ? Option.Some(connection.Activities[0]) : Option.None<ActivityView>();

    private string Unknown() => localisation.Get("Onboarding.Unknown");

    private string Gigabytes(long bytes) => (bytes / 1_073_741_824d).ToString("N1", culture.Culture);

    private void RaiseProgressChanged()
    {
        OnPropertyChanged(nameof(PhaseText));
        OnPropertyChanged(nameof(ItemCountsText));
        OnPropertyChanged(nameof(ThroughputText));
        OnPropertyChanged(nameof(RemainingText));
    }

    private string NameOf(string folderId) => Find(folders.Roots, folderId) ?? folderId;

    private static string? Find(IEnumerable<FolderNodeViewModel> rows, string folderId)
    {
        foreach (var row in rows)
        {
            if (row.Kind == FolderRowKind.Folder && row.Id == folderId) return row.Text;
            if (Find(row.Children, folderId) is { } name) return name;
        }

        return null;
    }

    private void OnSelectionChanged(PropertyChangedEventArgs? args)
    {
        if (args is not null && args.PropertyName != nameof(ScopeSelectionViewModel.LocalRoot)) return;

        RaiseContinueChanged();
    }

    private void RaiseContinueChanged()
    {
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsConnected));
    }
}
