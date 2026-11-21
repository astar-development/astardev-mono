using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Settings screen. Hosts the existing scope selection and the Settings &gt; Advanced entries until the settings slice replaces it.
/// The navigation service is requested lazily because it is built from the screens, this one included.
/// </summary>
internal sealed partial class SettingsViewModel(ILocalisationService localisation, Func<INavigationService> navigation, FolderTreeViewModel folders, ScopeSelectionViewModel selection) : ViewModelBase, IScreen
{
    private static readonly ScreenId[] AdvancedScreens = [ScreenId.Metrics, ScreenId.Logs, ScreenId.Traces, ScreenId.Diagnostics];

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Settings;

    /// <inheritdoc/>
    public string Title => localisation.Get("Settings.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Settings.Glyph");

    /// <summary>Localised heading of the Advanced section.</summary>
    public string AdvancedHeading => localisation.Get("Navigation.Advanced");

    /// <summary>Metrics, Logs, Traces and Diagnostics, in that order.</summary>
    public IReadOnlyList<DestinationViewModel> Advanced { get; } = AdvancedScreens.Select(id => new DestinationViewModel(id, localisation.Get($"{id}.Title"), localisation.Get($"{id}.Glyph"))).ToList();

    /// <summary>The remote folders, expanded level by level.</summary>
    public FolderTreeViewModel Folders => folders;

    /// <summary>The ticked folders with the local folder, bootstrap mode and Create job button.</summary>
    public ScopeSelectionViewModel Selection => selection;

    /// <summary>Opens the advanced screen named by the <see cref="ScreenId"/> parameter.</summary>
    [RelayCommand]
    private void OpenAdvanced(ScreenId screen) => navigation().Navigate(screen);
}
