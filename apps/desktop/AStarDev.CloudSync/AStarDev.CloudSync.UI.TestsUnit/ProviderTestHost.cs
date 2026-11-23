using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Providers;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class ProviderTestHost : IDisposable
{
    private readonly IHost host;

    private ProviderTestHost(IHost host, FakeIpcClient client, IProviderCatalogue? catalogue, FakeUriLauncher launcher)
    {
        this.host = host;
        Client = client;
        Launcher = launcher;
        var localisation = host.Services.GetRequiredService<ILocalisationService>();
        Connect = new ConnectViewModel(() => client);
        Accounts = new AccountsViewModel(() => client);
        Providers = new ProvidersViewModel(localisation, Connect, Accounts, host.Services.GetRequiredService<Func<INavigationService>>(), catalogue ?? new SampleProviderCatalogue(localisation), new SampleProviderInsightsSource(localisation), launcher);
    }

    public FakeIpcClient Client { get; }

    public FakeUriLauncher Launcher { get; }

    public ConnectViewModel Connect { get; }

    public AccountsViewModel Accounts { get; }

    public ProvidersViewModel Providers { get; }

    public INavigationService Navigation => host.Services.GetRequiredService<INavigationService>();

    public static ProviderTestHost Create(FakeIpcClient client, IProviderCatalogue? catalogue = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();
        var launcher = new FakeUriLauncher();
        builder.Services.AddSingleton<IUriLauncher>(launcher);

        return new ProviderTestHost(builder.Build(), client, catalogue, launcher);
    }

    public static AccountView Account(string name, string providerType = "OneDrive", AccountState state = AccountState.Connected) => new(Guid.NewGuid(), name, providerType, state);

    public static FakeIpcClient ClientWith(params AccountView[] accounts) => new([], available: true) { AccountsReply = new Ok<AccountsView>(new AccountsView(accounts)) };

    public void Dispose() => host.Dispose();
}
