using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Providers;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The Providers screen: a card per connected account, a health summary, the add-provider journey, removal, and a renewal warning.</summary>
internal sealed partial class ProvidersViewModel : ViewModelBase, IScreen
{
    private readonly ILocalisationService localisation;
    private readonly AccountsViewModel accounts;
    private readonly Func<INavigationService> navigation;
    private readonly IProviderCatalogue catalogue;
    private readonly IProviderInsightsSource insights;
    private readonly IUriLauncher launcher;

    /// <summary>Builds the screen with the sample catalogue and sample figures. Used by the application composition, which registers screens by assembly scan.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="connect">Performs the sign-in of the add journey.</param>
    /// <param name="accounts">The connected accounts the cards mirror.</param>
    /// <param name="navigation">Reaches the navigation service, which needs the screens that include this one.</param>
    /// <param name="launcher">Opens the help link of an error in the browser.</param>
    public ProvidersViewModel(ILocalisationService localisation, ConnectViewModel connect, AccountsViewModel accounts, Func<INavigationService> navigation, IUriLauncher launcher)
        : this(localisation, connect, accounts, navigation, new SampleProviderCatalogue(localisation), new SampleProviderInsightsSource(localisation), launcher)
    {
    }

    /// <summary>Builds the screen with the given provider metadata and figures.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="connect">Performs the sign-in of the add journey.</param>
    /// <param name="accounts">The connected accounts the cards mirror.</param>
    /// <param name="navigation">Reaches the navigation service.</param>
    /// <param name="catalogue">The provider metadata registry.</param>
    /// <param name="insights">The per-account figures.</param>
    /// <param name="launcher">Opens the help link of an error in the browser.</param>
    public ProvidersViewModel(ILocalisationService localisation, ConnectViewModel connect, AccountsViewModel accounts, Func<INavigationService> navigation, IProviderCatalogue catalogue, IProviderInsightsSource insights, IUriLauncher launcher)
    {
        this.localisation = localisation;
        this.accounts = accounts;
        this.navigation = navigation;
        this.catalogue = catalogue;
        this.insights = insights;
        this.launcher = launcher;
        Connect = connect;
        Add = new AddProviderViewModel(connect, catalogue, localisation);
        HealthSummary = BuildSummary();
        accounts.Accounts.CollectionChanged += (_, _) => Rebuild();
        connect.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectViewModel.ErrorMessage)) ShowError();
        };
        accounts.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AccountsViewModel.ErrorMessage)) ShowError();
        };
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Providers;

    /// <inheritdoc/>
    public string Title => localisation.Get("Providers.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Providers.Glyph");

    /// <summary>Busy state of the sign-in, which the view shows as a progress bar.</summary>
    public ConnectViewModel Connect { get; }

    /// <summary>Busy state of disconnect and reconnect requests.</summary>
    public AccountsViewModel Accounts => accounts;

    /// <summary>The add-provider journey.</summary>
    public AddProviderViewModel Add { get; }

    /// <summary>One card per connected account, oldest connection first. Rebuilt whenever the account list changes.</summary>
    public ObservableCollection<ProviderCardViewModel> Cards { get; } = [];

    /// <summary>How many accounts are in each <see cref="ProviderHealth"/>, always listing all six states.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProviderHealthCount> HealthSummary { get; set; }

    /// <summary>The removal flow while one is open; <see langword="null"/> otherwise.</summary>
    [ObservableProperty]
    public partial RemoveProviderViewModel? Removal { get; set; }

    /// <summary>The latest failure with guidance, or <see langword="null"/> when the last requests worked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial ProviderErrorViewModel? Error { get; set; }

    /// <summary>True while <see cref="Error"/> is set.</summary>
    public bool HasError => Error is not null;

    /// <summary>True while any account needs to sign in again. The warning stays until they do and never blocks the rest of the screen.</summary>
    public bool HasRenewalWarning => Cards.Any(card => card.CanReconnect);

    /// <summary>Names the accounts that need to sign in again.</summary>
    public string RenewalWarningText => localisation.Format("Providers.Renewal.Warning", ("accounts", string.Join(", ", Cards.Where(card => card.CanReconnect).Select(card => card.AccountName))));

    /// <summary>Signs in again through the browser, from the warning or a card. Also exposed to the view as <c>RenewSignInCommand</c>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait; the daemon may still finish the request.</param>
    [RelayCommand]
    public Task RenewSignInAsync(CancellationToken cancellationToken) => accounts.ReconnectAsync(cancellationToken);

    internal void Manage(AccountView account) => _ = navigation().Navigate(ScreenId.Settings, account.Id.ToString());

    internal void BeginRemove(AccountView account) => Removal = new RemoveProviderViewModel(
        localisation.Format("Providers.Remove.Title", ("account", account.DisplayName)),
        cancellationToken => accounts.DisconnectAsync(account, cancellationToken),
        () => Removal = null,
        localisation.Get("Providers.Remove.KeepsFiles"),
        localisation.Get("Providers.Remove.Recovery"));

    private void ShowError()
    {
        var message = Connect.ErrorMessage ?? accounts.ErrorMessage;
        var traceId = Option.FromNullable(Connect.ErrorMessage is null ? accounts.ErrorTraceId : Connect.ErrorTraceId);
        var code = Connect.ErrorMessage is null ? accounts.ErrorCode : Connect.ErrorCode;
        Error = message is null ? null : new ProviderErrorViewModel(message, traceId, code, launcher, localisation, () => _ = navigation().Navigate(ScreenId.Diagnostics));
    }

    private void Rebuild()
    {
        Cards.Clear();
        foreach (var account in accounts.Accounts.Select(card => card.Account)) Cards.Add(CreateCard(account));
        HealthSummary = BuildSummary();
        OnPropertyChanged(nameof(HasRenewalWarning));
        OnPropertyChanged(nameof(RenewalWarningText));
    }

    private ProviderCardViewModel CreateCard(AccountView account)
    {
        var figures = insights.Describe(account);
        var health = account.State == AccountState.ReauthenticationRequired ? ProviderHealth.AuthenticationRequired : figures.Health;

        return new ProviderCardViewModel(account, catalogue.Find(account.ProviderType), figures, ProviderHealthViewModel.For(health, localisation), this, localisation);
    }

    private List<ProviderHealthCount> BuildSummary() => [.. Enum.GetValues<ProviderHealth>().Select(health => new ProviderHealthCount(ProviderHealthViewModel.For(health, localisation), Cards.Count(card => card.Health.Health == health)))];
}
