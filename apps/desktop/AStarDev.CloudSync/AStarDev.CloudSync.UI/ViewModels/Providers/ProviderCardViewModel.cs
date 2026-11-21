using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>One connected account as a card. Which actions it offers follows the provider's capabilities, never its name.</summary>
/// <param name="account">What the daemon reported about the account.</param>
/// <param name="descriptor">The provider's name and capabilities.</param>
/// <param name="insights">The sample figures shown on the card.</param>
/// <param name="health">The account's health, already decided by the screen.</param>
/// <param name="owner">The screen, which carries out the actions.</param>
/// <param name="localisation">Supplies the capability and pause texts.</param>
internal sealed partial class ProviderCardViewModel(AccountView account, ProviderDescriptor descriptor, ProviderInsights insights, ProviderHealthViewModel health, ProvidersViewModel owner, ILocalisationService localisation) : ViewModelBase
{
    /// <summary>The account's name as the provider shows it. Each account has its own card, so several accounts of one provider stay distinct.</summary>
    public string AccountName => account.DisplayName;

    /// <summary>The provider's display name, from the metadata registry.</summary>
    public string ProviderName => descriptor.DisplayName;

    /// <summary>The account's health, drawn as colour, icon and text.</summary>
    public ProviderHealthViewModel Health => health;

    /// <summary>The features the provider offers.</summary>
    public IReadOnlyList<ProviderCapability> Capabilities => descriptor.Capabilities;

    /// <summary>The capabilities as localised words, for display.</summary>
    public IReadOnlyList<string> CapabilityLabels => [.. descriptor.Capabilities.Select(capability => localisation.Get($"Providers.Capability.{capability}"))];

    /// <summary>Last sync line, currently sample data.</summary>
    public string LastSyncText => insights.LastSyncText;

    /// <summary>Storage consumption line, currently sample data.</summary>
    public string StorageText => insights.StorageText;

    /// <summary>Recent activity line, currently sample data.</summary>
    public string RecentActivityText => insights.RecentActivityText;

    /// <summary>True when the provider supports selective sync, so folders can be managed.</summary>
    public bool CanManage => descriptor.Capabilities.Contains(ProviderCapability.SelectiveSync);

    /// <summary>True when the provider supports delta sync and sign-in is valid, so syncing can be paused.</summary>
    public bool CanPause => descriptor.Capabilities.Contains(ProviderCapability.DeltaSync) && !CanReconnect;

    /// <summary>True when the provider refused the stored sign-in, so the card offers Reconnect.</summary>
    public bool CanReconnect => health.Health == ProviderHealth.AuthenticationRequired;

    /// <summary>Always true: any account can be removed.</summary>
    public bool CanRemove => true;

    /// <summary>True while the user has paused this account. Does not change <see cref="Health"/>, and the daemon does not pause yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseLabel))]
    public partial bool IsPaused { get; set; }

    /// <summary>Pause or Resume, whichever the button now does.</summary>
    public string PauseLabel => localisation.Get(IsPaused ? "Providers.Card.Resume" : "Providers.Card.Pause");

    /// <summary>Opens the place where the account's folders are managed. Also exposed to the view as <c>ManageCommand</c>.</summary>
    [RelayCommand]
    public void Manage() => owner.Manage(account);

    /// <summary>Pauses or resumes the account. Also exposed to the view as <c>PauseCommand</c>.</summary>
    [RelayCommand]
    public void Pause() => IsPaused = !IsPaused;

    /// <summary>Signs in again through the browser. Also exposed to the view as <c>ReconnectCommand</c>.</summary>
    [RelayCommand]
    public Task ReconnectAsync(CancellationToken cancellationToken) => owner.RenewSignInAsync(cancellationToken);

    /// <summary>Starts the removal flow for this account. Also exposed to the view as <c>RemoveCommand</c>.</summary>
    [RelayCommand]
    public void Remove() => owner.BeginRemove(account);
}
