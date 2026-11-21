using AStarDev.CloudSync.UI.Localisation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>The add-provider journey. Signing in reuses <see cref="ConnectViewModel"/>; the steps after it describe what the daemon already did or will do.</summary>
/// <param name="connect">Performs the sign-in and verification at the Authenticate step.</param>
/// <param name="catalogue">Lists the providers to choose from.</param>
/// <param name="localisation">Supplies step titles and texts.</param>
internal sealed partial class AddProviderViewModel(ConnectViewModel connect, IProviderCatalogue catalogue, ILocalisationService localisation) : ViewModelBase
{
    /// <summary>True while the journey is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>The step being shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle), nameof(StepText), nameof(ActionText), nameof(IsSelecting))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial AddProviderStep Step { get; set; }

    /// <summary>The provider chosen, or <see langword="null"/> before a choice.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepText))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial ProviderDescriptor? SelectedProvider { get; set; }

    /// <summary>Every provider the user can pick, including placeholders that cannot be chosen.</summary>
    public IReadOnlyList<ProviderDescriptor> Providers => catalogue.All;

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

    /// <summary>Chooses <paramref name="provider"/>. A placeholder provider that is not available is ignored. Also exposed to the view as <c>SelectCommand</c>.</summary>
    /// <param name="provider">The provider the user picked.</param>
    [RelayCommand]
    public void Select(ProviderDescriptor provider)
    {
        if (provider.IsAvailable) SelectedProvider = provider;
    }

    /// <summary>
    /// Moves to the next step. At Authenticate it signs in first and stays there when that fails, leaving the reason on <see cref="ConnectViewModel.ErrorMessage"/>.
    /// At Activate it closes the journey. Cancellation surfaces as <see cref="OperationCanceledException"/>. Also exposed to the view as <c>ContinueCommand</c>.
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
            case AddProviderStep.Activate:
                IsOpen = false;
                break;
            default:
                Step++;
                break;
        }
    }

    private bool CanContinue() => Step != AddProviderStep.SelectProvider || SelectedProvider is not null;
}
