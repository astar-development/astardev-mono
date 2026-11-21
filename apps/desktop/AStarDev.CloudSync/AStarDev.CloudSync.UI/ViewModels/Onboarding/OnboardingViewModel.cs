using System.ComponentModel;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
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
    private readonly Func<INavigationService> navigation;
    private readonly AccountsViewModel accounts;
    private readonly ConnectViewModel connect;
    private readonly FolderTreeViewModel folders;
    private readonly ScopeSelectionViewModel selection;
    private OnboardingStep step = OnboardingStep.Welcome;
    private bool isActive;
    private bool evaluated;

    /// <summary>Starts listening: the first time the daemon link comes up, <see cref="EvaluateFirstRunCommand"/> decides whether to show the flow.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="navigation">Requested lazily, because the navigation service is built from the screens. Used to land on the Dashboard.</param>
    /// <param name="connection">The daemon link; a first connection triggers the first-run check.</param>
    /// <param name="accounts">Tells whether an account already exists, and names the connected provider on the review.</param>
    /// <param name="connect">Signs in to the provider.</param>
    /// <param name="folders">The remote folders, used to give the chosen scopes their names.</param>
    /// <param name="selection">The ticked folders and local folder; also creates the sync job on confirmation.</param>
    public OnboardingViewModel(ILocalisationService localisation, Func<INavigationService> navigation, ConnectionViewModel connection, AccountsViewModel accounts, ConnectViewModel connect, FolderTreeViewModel folders, ScopeSelectionViewModel selection)
    {
        this.localisation = localisation;
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

    /// <summary>Estimated item count, labelled as a placeholder until the daemon can supply it.</summary>
    public string EstimatedItemsText => localisation.Get("Onboarding.Review.EstimatedItemsPlaceholder");

    /// <summary>Available storage, labelled as a placeholder until the daemon can supply it.</summary>
    public string AvailableStorageText => localisation.Get("Onboarding.Review.AvailableStoragePlaceholder");

    /// <summary>Current phase of the initial synchronisation; a labelled placeholder.</summary>
    public string PhaseText => localisation.Get("Onboarding.Sync.PhasePlaceholder");

    /// <summary>Item counts of the initial synchronisation; a labelled placeholder.</summary>
    public string ItemCountsText => localisation.Get("Onboarding.Sync.ItemsPlaceholder");

    /// <summary>Throughput of the initial synchronisation; a labelled placeholder.</summary>
    public string ThroughputText => localisation.Get("Onboarding.Sync.ThroughputPlaceholder");

    /// <summary>Estimated remaining time of the initial synchronisation; a labelled placeholder.</summary>
    public string RemainingText => localisation.Get("Onboarding.Sync.RemainingPlaceholder");

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
