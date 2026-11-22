namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One entry of the Settings category list.</summary>
/// <param name="Category">The section the entry opens.</param>
/// <param name="Title">Localised name shown in the list.</param>
internal sealed record SettingsCategoryViewModel(SettingsCategory Category, string Title);
