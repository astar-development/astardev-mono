using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class SettingsStartupShould
{
    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    [Fact]
    public void apply_the_saved_settings_once_the_daemon_is_connected()
    {
        var client = new FakeIpcClient([], available: true) { SettingsReply = new Ok<SettingsView>(new SettingsView(new Dictionary<string, string> { [SettingKeys.Theme] = "Light", [SettingKeys.NotifyOnCompletion] = "true" })) };
        var theme = new FakeThemeService();
        var notifications = new FakeNotificationService();
        var selection = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        var settings = new SettingsViewModel(RealLocalisation(), () => new FakeNavigationService(), new FolderTreeViewModel(() => client, selection), selection, new AccountsViewModel(() => client), () => client, theme, () => notifications);
        var connection = new ConnectionViewModel(() => client, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        _ = new SettingsStartup(connection, [settings]);

        theme.Current.ShouldBeNull();
        connection.State = ConnectionState.Connected;

        theme.Current.ShouldBe("Light");
        notifications.Preferences.Completion.ShouldBeTrue();
    }
}
