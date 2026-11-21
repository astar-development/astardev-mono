using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The Providers screen. Hosts the existing connect button and account cards until the providers slice replaces it.</summary>
internal sealed class ProvidersViewModel(ILocalisationService localisation, ConnectViewModel connect, AccountsViewModel accounts) : ViewModelBase, IScreen
{
    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Providers;

    /// <inheritdoc/>
    public string Title => localisation.Get("Providers.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Providers.Glyph");

    /// <summary>Connect button, busy state, item count and errors.</summary>
    public ConnectViewModel Connect => connect;

    /// <summary>The connected accounts with their Disconnect and Reconnect buttons.</summary>
    public AccountsViewModel Accounts => accounts;
}
