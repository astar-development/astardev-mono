using System.Globalization;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.Theming;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Settings screen with its six categories. Synchronisation hosts the scope selection and job form, Providers links to the Providers screen,
/// Advanced lists the technical screens, Appearance holds the theme, Notifications the event switches, and General the retention periods.
/// Values live in the daemon: <see cref="LoadAsync"/> reads and applies them, <see cref="SaveAsync"/> sends what changed and applies it.
/// The navigation service is requested lazily, as is the notification service, because both are built from the screens, this one included.
/// </summary>
internal sealed partial class SettingsViewModel(ILocalisationService localisation, Func<INavigationService> navigation, FolderTreeViewModel folders, ScopeSelectionViewModel selection, Func<IIpcClient> createClient, IThemeService themes, Func<INotificationService> notifications) : ViewModelBase, IScreen
{
    private static readonly ScreenId[] AdvancedScreens = [ScreenId.Metrics, ScreenId.Logs, ScreenId.Traces, ScreenId.Diagnostics];

    private Dictionary<string, string> saved = new()
    {
        [SettingKeys.Theme] = "Dark",
        [SettingKeys.NotifyOnConflict] = "true",
        [SettingKeys.NotifyOnError] = "true",
        [SettingKeys.NotifyOnCompletion] = "false",
        [SettingKeys.AuditRetentionDays] = "90",
        [SettingKeys.TelemetryRetentionDays] = "7"
    };

    private SettingsCategoryViewModel? selectedCategory;

    /// <summary>The theme chosen, one of <see cref="Themes"/>. Dark until the daemon's value is loaded; applied by <see cref="SaveAsync"/>.</summary>
    [ObservableProperty]
    public partial string Theme { get; set; } = "Dark";

    /// <summary>Whether a new conflict notifies.</summary>
    [ObservableProperty]
    public partial bool NotifyOnConflict { get; set; } = true;

    /// <summary>Whether a failed operation notifies.</summary>
    [ObservableProperty]
    public partial bool NotifyOnError { get; set; } = true;

    /// <summary>Whether a finished sync notifies.</summary>
    [ObservableProperty]
    public partial bool NotifyOnCompletion { get; set; }

    /// <summary>Days audit entries are kept. The daemon rejects a value below 1 when saved.</summary>
    [ObservableProperty]
    public partial decimal AuditRetentionDays { get; set; } = 90;

    /// <summary>Days telemetry is kept. The daemon rejects a value below 1 when saved.</summary>
    [ObservableProperty]
    public partial decimal TelemetryRetentionDays { get; set; } = 7;

    /// <summary>Why the last load or save failed, worded for the user. <see langword="null"/> after one that worked.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>The themes that can be chosen.</summary>
    public IReadOnlyList<string> Themes => themes.Available;

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Settings;

    /// <inheritdoc/>
    public string Title => localisation.Get("Settings.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Settings.Glyph");

    /// <summary>General, Providers, Synchronisation, Appearance, Notifications and Advanced, in that order.</summary>
    public IReadOnlyList<SettingsCategoryViewModel> Categories { get; } = Enum.GetValues<SettingsCategory>().Select(category => new SettingsCategoryViewModel(category, localisation.Get($"Settings.Category.{category}"))).ToList();

    /// <summary>The category whose section is shown. General until changed; assigning <see langword="null"/> is ignored so the list can never leave the screen with no section.</summary>
    public SettingsCategoryViewModel SelectedCategory
    {
        get => selectedCategory ?? Categories[0];
        set
        {
            if (value is null || !SetProperty(ref selectedCategory, value)) return;

            OnPropertyChanged(nameof(IsGeneral));
            OnPropertyChanged(nameof(IsProviders));
            OnPropertyChanged(nameof(IsSynchronisation));
            OnPropertyChanged(nameof(IsAppearance));
            OnPropertyChanged(nameof(IsNotifications));
            OnPropertyChanged(nameof(IsAdvanced));
        }
    }

    /// <summary>True while the General section is shown.</summary>
    public bool IsGeneral => SelectedCategory.Category == SettingsCategory.General;

    /// <summary>True while the Providers section is shown.</summary>
    public bool IsProviders => SelectedCategory.Category == SettingsCategory.Providers;

    /// <summary>True while the Synchronisation section is shown.</summary>
    public bool IsSynchronisation => SelectedCategory.Category == SettingsCategory.Synchronisation;

    /// <summary>True while the Appearance section is shown.</summary>
    public bool IsAppearance => SelectedCategory.Category == SettingsCategory.Appearance;

    /// <summary>True while the Notifications section is shown.</summary>
    public bool IsNotifications => SelectedCategory.Category == SettingsCategory.Notifications;

    /// <summary>True while the Advanced section is shown.</summary>
    public bool IsAdvanced => SelectedCategory.Category == SettingsCategory.Advanced;

    /// <summary>Localised heading of the Advanced section.</summary>
    public string AdvancedHeading => localisation.Get("Navigation.Advanced");

    /// <summary>Metrics, Logs, Traces and Diagnostics, in that order.</summary>
    public IReadOnlyList<DestinationViewModel> Advanced { get; } = AdvancedScreens.Select(id => new DestinationViewModel(id, localisation.Get($"{id}.Title"), localisation.Get($"{id}.Glyph"))).ToList();

    /// <summary>Placeholder entry for managing sync scopes after setup; explains what adding or removing a folder means.</summary>
    public SettingsEntryViewModel ScopeManagement { get; } = new(localisation.Get("Settings.ScopeManagement.Heading"), localisation.Get("Settings.ScopeManagement.Consequences"), localisation.Get("Settings.ScopeManagement.WhatHappensNext"));

    /// <summary>Placeholder entry for changing the sync root; explains what a change affects and that it is not available yet.</summary>
    public SettingsEntryViewModel SyncRootChange { get; } = new(localisation.Get("Settings.SyncRoot.Heading"), localisation.Get("Settings.SyncRoot.Consequences"), localisation.Get("Settings.SyncRoot.WhatHappensNext"));

    /// <summary>The remote folders, expanded level by level.</summary>
    public FolderTreeViewModel Folders => folders;

    /// <summary>The ticked folders with the local folder, bootstrap mode and Create job button.</summary>
    public ScopeSelectionViewModel Selection => selection;

    /// <summary>Reads every setting from the daemon, shows it and applies the theme and notification preferences. A failure keeps what is shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        ErrorMessage = await FetchAsync(client, cancellationToken) switch
        {
            Ok<SettingsView> ok => Show(ok.Value),
            Fail<ConnectFailure> fail => fail.Error.Message
        };
    }

    /// <summary>
    /// Sends every setting that differs from the daemon's value, then applies the theme and notification preferences. A value the daemon declines stops the save, lands in <see cref="ErrorMessage"/> and applies nothing;
    /// values sent before it stay saved.
    /// </summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    [RelayCommand]
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        ErrorMessage = await SendChangesAsync(client, cancellationToken) switch
        {
            Ok<Unit> => Apply(),
            Fail<ConnectFailure> fail => fail.Error.Message
        };
    }

    /// <inheritdoc/>
    public void OnNavigatedTo(string? targetId) => LoadCommand.Execute(null);

    private (string Name, string Value)[] Current() =>
    [
        (SettingKeys.Theme, Theme),
        (SettingKeys.NotifyOnConflict, Flag(NotifyOnConflict)),
        (SettingKeys.NotifyOnError, Flag(NotifyOnError)),
        (SettingKeys.NotifyOnCompletion, Flag(NotifyOnCompletion)),
        (SettingKeys.AuditRetentionDays, Days(AuditRetentionDays)),
        (SettingKeys.TelemetryRetentionDays, Days(TelemetryRetentionDays))
    ];

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Days(decimal value) => decimal.Round(value).ToString("0", CultureInfo.InvariantCulture);

    private static async Task<Result<SettingsView, ConnectFailure>> FetchAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await QueryAsync(client, cancellationToken),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<SettingsView, ConnectFailure>> QueryAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetSettings(), cancellationToken) switch
        {
            Ok<SettingsView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<Unit, ConnectFailure>> SendChangesAsync(IIpcClient client, CancellationToken cancellationToken)
    {
        if ((await client.ConnectAsync(cancellationToken)).Value is Fail<IpcError> unreachable) return new Fail<ConnectFailure>(ConnectFailure.From(unreachable.Error));

        foreach (var (name, value) in Current().Where(setting => saved[setting.Name] != setting.Value))
        {
            if ((await SendAsync(client, name, value, cancellationToken)).Value is Fail<ConnectFailure> failure) return failure;

            saved[name] = value;
        }

        return new Ok<Unit>(default);
    }

    private async Task<Result<Unit, ConnectFailure>> SendAsync(IIpcClient client, string name, string value, CancellationToken cancellationToken)
        => await client.SendAsync(new SetSetting(name, value), cancellationToken) switch
        {
            Ok<CommandResult> { Value.Outcome: CommandOutcome.Accepted } => new Ok<Unit>(default),
            Ok<CommandResult> rejected => new Fail<ConnectFailure>(ConnectFailure.Rejected(rejected.Value, localisation.Get("Settings.SaveRejected"))),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private string? Show(SettingsView view)
    {
        Theme = Text(view, SettingKeys.Theme, Theme);
        NotifyOnConflict = Flag(view, SettingKeys.NotifyOnConflict, NotifyOnConflict);
        NotifyOnError = Flag(view, SettingKeys.NotifyOnError, NotifyOnError);
        NotifyOnCompletion = Flag(view, SettingKeys.NotifyOnCompletion, NotifyOnCompletion);
        AuditRetentionDays = Number(view, SettingKeys.AuditRetentionDays, AuditRetentionDays);
        TelemetryRetentionDays = Number(view, SettingKeys.TelemetryRetentionDays, TelemetryRetentionDays);
        saved = Current().ToDictionary(setting => setting.Name, setting => setting.Value);

        return Apply();
    }

    private static string Text(SettingsView view, string name, string fallback) => view.Values.GetValueOrDefault(name, fallback);

    private static bool Flag(SettingsView view, string name, bool fallback) => view.Values.TryGetValue(name, out var text) && bool.TryParse(text, out var value) ? value : fallback;

    private static decimal Number(SettingsView view, string name, decimal fallback) => view.Values.TryGetValue(name, out var text) && int.TryParse(text, out var value) ? value : fallback;

    private string? Apply()
    {
        notifications().Preferences = new NotificationPreferences(NotifyOnConflict, NotifyOnError, NotifyOnCompletion);

        return themes.Apply(Theme).Value is Fail<ThemeError> ? localisation.Format("Settings.ThemeFailed", ("theme", Theme)) : null;
    }

    /// <summary>Opens the advanced screen named by the <see cref="ScreenId"/> parameter.</summary>
    [RelayCommand]
    private void OpenAdvanced(ScreenId screen) => navigation().Navigate(screen);

    /// <summary>Opens the Providers screen, where accounts are added, reconnected and removed.</summary>
    [RelayCommand]
    private void OpenProviders() => navigation().Navigate(ScreenId.Providers);
}
