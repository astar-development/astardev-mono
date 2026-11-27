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

        return (new SettingsViewModel(RealLocalisation(), () => navigation, new FolderTreeViewModel(() => client, selection), selection, new AccountsViewModel(() => client), () => client, theme, () => notifications), navigation);
    }

    private static readonly Guid OtherAccount = new("22222222-2222-2222-2222-222222222222");

    private static FakeIpcClient TwoAccounts() => new([], available: true)
    {
        AccountsReply = new Ok<AccountsView>(new AccountsView([new AccountView(FakeIpcClient.KnownAccount, "First", "OneDrive", AccountState.Connected), new AccountView(OtherAccount, "Second", "OneDrive", AccountState.Connected, 6)])),
        JobsReply = new Ok<SyncJobsView>(new SyncJobsView([new SyncJobView(Guid.NewGuid(), FakeIpcClient.KnownAccount, "First", "/home/first", SyncBootstrapMode.Merge), new SyncJobView(Guid.NewGuid(), OtherAccount, "Second", "/home/second", SyncBootstrapMode.Download)]))
    };

    private static async Task<SettingsViewModel> OpenedForAsync(FakeIpcClient client, string? targetId)
    {
        var selection = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        var accounts = new AccountsViewModel(() => client);
        await accounts.RefreshAsync(TestContext.Current.CancellationToken);
        var navigation = new FakeNavigationService();
        var settings = new SettingsViewModel(RealLocalisation(), () => navigation, new FolderTreeViewModel(() => client, selection), selection, accounts, () => client, new FakeThemeService(), () => new FakeNotificationService());
        settings.OnNavigatedTo(targetId);
        await (selection.RefreshCommand.ExecutionTask ?? Task.CompletedTask);

        return settings;
    }

    [Fact]
    public async Task show_the_job_of_the_account_it_was_opened_for()
    {
        var settings = await OpenedForAsync(TwoAccounts(), OtherAccount.ToString());

        settings.Selection.AccountId.ShouldBe(OtherAccount);
        settings.Selection.LocalRoot.ShouldBe("/home/second");
        settings.Selection.BootstrapMode.ShouldBe(SyncBootstrapMode.Download);
    }

    [Fact]
    public async Task open_the_synchronisation_section_when_opened_for_an_account()
    {
        var settings = await OpenedForAsync(TwoAccounts(), OtherAccount.ToString());

        settings.IsSynchronisation.ShouldBeTrue();
    }

    [Fact]
    public async Task name_the_account_it_was_opened_for()
    {
        var settings = await OpenedForAsync(TwoAccounts(), OtherAccount.ToString());

        settings.AccountName.ShouldBe("Second");
    }

    [Fact]
    public async Task keep_the_chosen_account_when_opened_again_without_one()
    {
        var settings = await OpenedForAsync(TwoAccounts(), OtherAccount.ToString());

        settings.OnNavigatedTo(null);
        await (settings.Selection.RefreshCommand.ExecutionTask ?? Task.CompletedTask);

        settings.Selection.AccountId.ShouldBe(OtherAccount);
        settings.Selection.LocalRoot.ShouldBe("/home/second");
    }

    [Fact]
    public async Task show_the_concurrency_of_the_account_it_was_opened_for()
    {
        var settings = await OpenedForAsync(TwoAccounts(), OtherAccount.ToString());

        settings.MaxConcurrentOperations.ShouldBe(6);
        settings.ConcurrencyPosition.ShouldBe(3);
    }

    [Fact]
    public async Task send_nothing_when_only_showing_the_concurrency()
    {
        var client = TwoAccounts();

        await OpenedForAsync(client, OtherAccount.ToString());

        client.Sent.OfType<SetAccountConcurrency>().ShouldBeEmpty();
    }

    [Fact]
    public async Task send_the_new_concurrency_of_the_account_when_the_slider_moves()
    {
        var client = TwoAccounts();
        var settings = await OpenedForAsync(client, OtherAccount.ToString());

        settings.ConcurrencyPosition = 4;
        await (settings.SendConcurrencyCommand.ExecutionTask ?? Task.CompletedTask);

        client.Sent.OfType<SetAccountConcurrency>().ShouldBe([new SetAccountConcurrency(OtherAccount, 8)]);
        settings.ErrorMessage.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1.4, 2)]
    [InlineData(2.6, 6)]
    [InlineData(9, 8)]
    public async Task snap_the_slider_to_an_allowed_concurrency(double position, int expected)
    {
        var settings = await OpenedForAsync(new FakeIpcClient([], available: true), null);

        settings.ConcurrencyPosition = position;

        settings.MaxConcurrentOperations.ShouldBe(expected);
    }

    [Fact]
    public async Task go_back_to_the_stored_concurrency_when_the_daemon_refuses_it()
    {
        var client = TwoAccounts();
        var settings = await OpenedForAsync(client, OtherAccount.ToString());
        client.CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "not allowed"));

        settings.ConcurrencyPosition = 0;
        await (settings.SendConcurrencyCommand.ExecutionTask ?? Task.CompletedTask);

        settings.ErrorMessage.ShouldBe("not allowed");
        settings.MaxConcurrentOperations.ShouldBe(6);
    }

    [Fact]
    public async Task honour_cancellation_when_sending_the_concurrency()
    {
        var settings = await OpenedForAsync(TwoAccounts(), OtherAccount.ToString());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => settings.SendConcurrencyAsync(cancelled.Token));
    }

    [Fact]
    public async Task show_the_first_account_when_opened_without_one_and_none_was_chosen()
    {
        var settings = await OpenedForAsync(TwoAccounts(), null);

        settings.Selection.AccountId.ShouldBe(FakeIpcClient.KnownAccount);
        settings.AccountName.ShouldBe("First");
    }

    private static FakeIpcClient WithSettings(params (string Name, string Value)[] values) => new([], available: true) { SettingsReply = new Ok<SettingsView>(new SettingsView(values.ToDictionary(value => value.Name, value => value.Value))) };

    [Fact]
    public void ask_for_confirmation_before_stopping_the_daemon_and_send_nothing_yet()
    {
        var client = new FakeIpcClient([], available: true);
        var (settings, _) = Create(client, new FakeThemeService(), new FakeNotificationService());

        settings.RequestStopDaemonCommand.Execute(null);

        settings.IsDaemonConfirmationShown.ShouldBeTrue();
        settings.DaemonConfirmationText.ShouldContain("Stop");
        client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task stop_the_daemon_once_confirmed()
    {
        var client = new FakeIpcClient([], available: true);
        var (settings, _) = Create(client, new FakeThemeService(), new FakeNotificationService());
        settings.RequestStopDaemonCommand.Execute(null);

        await settings.ConfirmDaemonRequestAsync(TestContext.Current.CancellationToken);

        client.Sent.ShouldBe([new StopDaemon(false)]);
        settings.IsDaemonConfirmationShown.ShouldBeFalse();
        settings.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task restart_the_daemon_once_confirmed()
    {
        var client = new FakeIpcClient([], available: true);
        var (settings, _) = Create(client, new FakeThemeService(), new FakeNotificationService());
        settings.RequestRestartDaemonCommand.Execute(null);

        await settings.ConfirmDaemonRequestAsync(TestContext.Current.CancellationToken);

        client.Sent.ShouldBe([new StopDaemon(true)]);
    }

    [Fact]
    public void send_nothing_when_the_daemon_request_is_cancelled()
    {
        var client = new FakeIpcClient([], available: true);
        var (settings, _) = Create(client, new FakeThemeService(), new FakeNotificationService());
        settings.RequestRestartDaemonCommand.Execute(null);

        settings.CancelDaemonRequestCommand.Execute(null);

        settings.IsDaemonConfirmationShown.ShouldBeFalse();
        client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task show_why_the_daemon_declined_to_stop()
    {
        var client = new FakeIpcClient([], available: true) { CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "Busy.")) };
        var (settings, _) = Create(client, new FakeThemeService(), new FakeNotificationService());
        settings.RequestStopDaemonCommand.Execute(null);

        await settings.ConfirmDaemonRequestAsync(TestContext.Current.CancellationToken);

        settings.ErrorMessage.ShouldBe("Busy.");
    }

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
