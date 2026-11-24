using AStarDev.CloudSync.UI.Localisation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>The add-provider journey. Signing in reuses <see cref="ConnectViewModel"/>; the steps after it describe what the daemon already did or will do.</summary>
/// <param name="connect">Performs the sign-in and verification at the Authenticate step.</param>
/// <param name="catalogue">Lists the providers to choose from.</param>
/// <param name="localisation">Supplies step titles and texts.</param>
/// <param name="folders">The remote folder tree shown at the sync root step.</param>
/// <param name="selection">The ticked folders and local folder; Continue at the sync root step creates the sync job from them.</param>
internal sealed partial class AddProviderViewModel(ConnectViewModel connect, IProviderCatalogue catalogue, ILocalisationService localisation, FolderTreeViewModel folders, ScopeSelectionViewModel selection) : ViewModelBase
{
    /// <summary>True while the journey is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>The step being shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle), nameof(StepText), nameof(ActionText), nameof(IsSelecting), nameof(IsChoosingSyncRoot))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial AddProviderStep Step { get; set; }

    /// <summary>The provider chosen, or <see langword="null"/> before a choice.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepText))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial ProviderDescriptor? SelectedProvider { get; set; }

    /// <summary>Every provider the user can pick, .</summary>
    public IReadOnlyList<ProviderDescriptor> Providers => catalogue.All;

    /// <summary>Tells the view the provider list may have changed, after the catalogue was refreshed.</summary>
    public void Refresh() => OnPropertyChanged(nameof(Providers));

    /// <summary>The remote folders to tick at the sync root step.</summary>
    public FolderTreeViewModel Folders => folders;

    /// <summary>The local folder and ticked folders at the sync root step.</summary>
    public ScopeSelectionViewModel Selection => selection;

    /// <summary>True at the sync root step, where the folder tree and local folder are shown.</summary>
    public bool IsChoosingSyncRoot => Step == AddProviderStep.ChooseSyncRoot;

    /// <summary>True while the provider list is shown, that is at the first step.</summary>
    public bool IsSelecting => Step == AddProviderStep.SelectProvider;

    /// <summary>The heading of <see cref="Step"/>.</summary>
    public string StepTitle => localisation.Get($"Providers.Add.Step.{Step}.Title");

    /// <summary>The explanation of <see cref="Step"/>, including the verified item count at the verify step.</summary>
    public string StepText => localisation.Format($"Providers.Add.Step.{Step}.Text", ("provider", SelectedProvider?.DisplayName), ("items", connect.ItemCountText));

    /// <summary>The label of the button that moves past <see cref="Step"/>.</summary>
    public string ActionText => localisation.Get($"Providers.Add.Step.{Step}.Action");

    /// <summary>Opens the journey at the first step with nothing chosen. Also exposed to the view as <c>BeginCommand</c>.</summary>
    [RelayCommand]
    public void Begin()
    {
        SelectedProvider = null;
        Step = AddProviderStep.SelectProvider;
        IsOpen = true;
    }

    /// <summary>Closes the journey and forgets the choice. Also exposed to the view as <c>CancelCommand</c>.</summary>
    [RelayCommand]
    public void Cancel()
    {
        IsOpen = false;
        SelectedProvider = null;
    }

    /// <summary>Chooses <paramref name="provider"/>. Also exposed to the view as <c>SelectCommand</c>.</summary>
    /// <param name="provider">The provider the user picked.</param>
    [RelayCommand]
    public void Select(ProviderDescriptor provider) => SelectedProvider = provider;

    /// <summary>
    /// Moves to the next step. At Authenticate it signs in first and stays there when that fails, leaving the reason on <see cref="ConnectViewModel.ErrorMessage"/>.
    /// At the sync root step it creates the sync job when a local folder and folders are chosen, and stays there when that fails, leaving the reason on <see cref="ScopeSelectionViewModel.StatusMessage"/>; with nothing chosen it moves on. At Activate it closes the journey. Cancellation surfaces as <see cref="OperationCanceledException"/>. Also exposed to the view as <c>ContinueCommand</c>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanContinue))]
    public async Task ContinueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (Step)
        {
            case AddProviderStep.Authenticate:
                await connect.ConnectAsync(cancellationToken);
                if (connect.ErrorMessage is null) Step = AddProviderStep.GrantPermissions;
                break;
            case AddProviderStep.ChooseSyncRoot:
                await CreateJobAsync(cancellationToken);
                break;
            case AddProviderStep.Activate:
                IsOpen = false;
                break;
            default:
                Step++;
                break;
        }
    }

    partial void OnStepChanged(AddProviderStep value)
    {
        if (value != AddProviderStep.ChooseSyncRoot) return;

        selection.RefreshCommand.Execute(null);
        folders.RefreshCommand.Execute(null);
    }

    private async Task CreateJobAsync(CancellationToken cancellationToken)
    {
        await selection.CreateJobAsync(cancellationToken);
        if (selection.StatusMessage is null) Step++;
    }

    private bool CanContinue() => Step != AddProviderStep.SelectProvider || SelectedProvider is not null;
}
