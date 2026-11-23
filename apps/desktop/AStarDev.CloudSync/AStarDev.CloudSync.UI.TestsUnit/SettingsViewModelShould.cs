using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class SettingsViewModelShould
{
    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    private static (SettingsViewModel Settings, FakeNavigationService Navigation) Create() => Create(new FakeIpcClient([], available: true), new FakeThemeService(), new FakeNotificationService());

    private static (SettingsViewModel Settings, FakeNavigationService Navigation) Create(FakeIpcClient client, FakeThemeService theme, FakeNotificationService notifications)
    {
        var selection = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        var navigation = new FakeNavigationService();

        return (new SettingsViewModel(RealLocalisation(), () => navigation, new FolderTreeViewModel(() => client, selection), selection, () => client, theme, () => notifications), navigation);
    }

    private static FakeIpcClient WithSettings(params (string Name, string Value)[] values) => new([], available: true) { SettingsReply = new Ok<SettingsView>(new SettingsView(values.ToDictionary(value => value.Name, value => value.Value))) };

    [Fact]
    public async Task load_the_values_from_the_daemon()
    {
        var client = WithSettings((SettingKeys.Theme, "Light"), (SettingKeys.NotifyOnConflict, "false"), (SettingKeys.NotifyOnError, "true"), (SettingKeys.NotifyOnCompletion, "true"), (SettingKeys.AuditRetentionDays, "30"), (SettingKeys.TelemetryRetentionDays, "3"));
        var (settings, _) = Create(client, new FakeThemeService(), new FakeNotificationService());

        await settings.LoadAsync(TestContext.Current.CancellationToken);

        settings.Theme.ShouldBe("Light");
        settings.NotifyOnConflict.ShouldBeFalse();
        settings.NotifyOnError.ShouldBeTrue();
        settings.NotifyOnCompletion.ShouldBeTrue();
        settings.AuditRetentionDays.ShouldBe(30);
        settings.TelemetryRetentionDays.ShouldBe(3);
        settings.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task apply_the_loaded_theme_and_notification_preferences()
    {
        var theme = new FakeThemeService();
        var notifications = new FakeNotificationService();
        var (settings, _) = Create(WithSettings((SettingKeys.Theme, "Light"), (SettingKeys.NotifyOnCompletion, "true"), (SettingKeys.NotifyOnConflict, "false"), (SettingKeys.NotifyOnError, "true")), theme, notifications);

        await settings.LoadAsync(TestContext.Current.CancellationToken);

        theme.Current.ShouldBe("Light");
        notifications.Preferences.ShouldBe(new NotificationPreferences(false, true, true));
    }

    [Fact]
    public async Task keep_the_defaults_and_report_when_the_daemon_is_unreachable()
    {
        var (settings, _) = Create(new FakeIpcClient([], available: false), new FakeThemeService(), new FakeNotificationService());

        await settings.LoadAsync(TestContext.Current.CancellationToken);

        settings.ErrorMessage.ShouldBe(ApplicationConstants.DaemonUnreachableText);
        settings.Theme.ShouldBe("Dark");
    }

    [Fact]
    public async Task save_a_changed_theme()
    {
        var client = WithSettings((SettingKeys.Theme, "Dark"));
        var theme = new FakeThemeService();
        var (settings, _) = Create(client, theme, new FakeNotificationService());
        await settings.LoadAsync(TestContext.Current.CancellationToken);

        settings.Theme = "Light";
        await settings.SaveAsync(TestContext.Current.CancellationToken);

        client.Sent.ShouldBe([new SetSetting(SettingKeys.Theme, "Light")]);
        theme.Current.ShouldBe("Light");
        settings.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task apply_notification_preferences()
    {
        var client = WithSettings();
        var notifications = new FakeNotificationService();
        var (settings, _) = Create(client, new FakeThemeService(), notifications);
        await settings.LoadAsync(TestContext.Current.CancellationToken);

        settings.NotifyOnCompletion = true;
        settings.NotifyOnConflict = false;
        await settings.SaveAsync(TestContext.Current.CancellationToken);

        notifications.Preferences.ShouldBe(new NotificationPreferences(false, true, true));
        client.Sent.ShouldBe([new SetSetting(SettingKeys.NotifyOnConflict, "false"), new SetSetting(SettingKeys.NotifyOnCompletion, "true")]);
    }

    [Fact]
    public async Task send_changed_retention_to_the_daemon()
    {
        var client = WithSettings();
        var (settings, _) = Create(client, new FakeThemeService(), new FakeNotificationService());
        await settings.LoadAsync(TestContext.Current.CancellationToken);

        settings.AuditRetentionDays = 14;
        settings.TelemetryRetentionDays = 2;
        await settings.SaveAsync(TestContext.Current.CancellationToken);

        client.Sent.ShouldBe([new SetSetting(SettingKeys.AuditRetentionDays, "14"), new SetSetting(SettingKeys.TelemetryRetentionDays, "2")]);
    }

    [Fact]
    public async Task show_the_daemon_message_and_apply_nothing_when_a_value_is_rejected()
    {
        var client = WithSettings();
        client.CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "Retention must be at least 1 day."));
        var theme = new FakeThemeService();
        var (settings, _) = Create(client, theme, new FakeNotificationService());
        await settings.LoadAsync(TestContext.Current.CancellationToken);

        settings.AuditRetentionDays = 0;
        settings.Theme = "Light";
        await settings.SaveAsync(TestContext.Current.CancellationToken);

        settings.ErrorMessage.ShouldBe("Retention must be at least 1 day.");
        theme.Current.ShouldBe("Dark");
    }

    [Fact]
    public Task cancel_a_save()
    {
        var (settings, _) = Create();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        return Should.ThrowAsync<OperationCanceledException>(() => settings.SaveAsync(cancelled.Token));
    }

    [Fact]
    public Task cancel_a_load()
    {
        var (settings, _) = Create();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        return Should.ThrowAsync<OperationCanceledException>(() => settings.LoadAsync(cancelled.Token));
    }

    [Fact]
    public void expose_the_six_categories()
    {
        var (settings, _) = Create();

        settings.Categories.Select(category => category.Category).ShouldBe([SettingsCategory.General, SettingsCategory.Providers, SettingsCategory.Synchronisation, SettingsCategory.Appearance, SettingsCategory.Notifications, SettingsCategory.Advanced]);
        foreach (var category in settings.Categories) category.Title.ShouldNotStartWith("!");
    }

    [Fact]
    public void start_on_general_and_show_only_the_selected_category()
    {
        var (settings, _) = Create();

        settings.SelectedCategory.Category.ShouldBe(SettingsCategory.General);
        settings.IsGeneral.ShouldBeTrue();
        settings.IsSynchronisation.ShouldBeFalse();

        settings.SelectedCategory = settings.Categories.Single(category => category.Category == SettingsCategory.Synchronisation);

        settings.IsGeneral.ShouldBeFalse();
        settings.IsSynchronisation.ShouldBeTrue();
    }

    [Fact]
    public void open_the_providers_screen_from_the_providers_category()
    {
        var (settings, navigation) = Create();

        settings.OpenProvidersCommand.Execute(null);

        navigation.Visited.ShouldBe([ScreenId.Providers]);
    }

    [Fact]
    public void open_an_advanced_screen()
    {
        var (settings, navigation) = Create();

        settings.OpenAdvancedCommand.Execute(ScreenId.Logs);

        navigation.Visited.ShouldBe([ScreenId.Logs]);
    }
}
