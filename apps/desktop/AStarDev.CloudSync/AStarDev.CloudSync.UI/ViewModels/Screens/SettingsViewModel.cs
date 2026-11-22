using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Settings screen with its six categories. Synchronisation hosts the scope selection and job form, Providers links to the Providers screen,
/// Advanced lists the technical screens, and Appearance and Notifications are labelled placeholders.
/// The navigation service is requested lazily because it is built from the screens, this one included.
/// </summary>
internal sealed partial class SettingsViewModel(ILocalisationService localisation, Func<INavigationService> navigation, FolderTreeViewModel folders, ScopeSelectionViewModel selection) : ViewModelBase, IScreen
{
    private static readonly ScreenId[] AdvancedScreens = [ScreenId.Metrics, ScreenId.Logs, ScreenId.Traces, ScreenId.Diagnostics];

    private SettingsCategoryViewModel? selectedCategory;

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

    /// <summary>Opens the advanced screen named by the <see cref="ScreenId"/> parameter.</summary>
    [RelayCommand]
    private void OpenAdvanced(ScreenId screen) => navigation().Navigate(screen);

    /// <summary>Opens the Providers screen, where accounts are added, reconnected and removed.</summary>
    [RelayCommand]
    private void OpenProviders() => navigation().Navigate(ScreenId.Providers);
}
