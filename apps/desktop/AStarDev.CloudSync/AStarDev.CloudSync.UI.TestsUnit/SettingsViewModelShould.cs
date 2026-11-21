using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class SettingsViewModelShould
{
    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    private static (SettingsViewModel Settings, FakeNavigationService Navigation) Create()
    {
        var client = new FakeIpcClient([], available: true);
        var selection = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        var navigation = new FakeNavigationService();

        return (new SettingsViewModel(RealLocalisation(), () => navigation, new FolderTreeViewModel(() => client, selection), selection), navigation);
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
    public void explain_consequences_and_what_happens_next_for_the_placeholder_entry_points()
    {
        var (settings, _) = Create();

        foreach (var entry in new[] { settings.ScopeManagement, settings.SyncRootChange })
        {
            entry.Heading.ShouldNotStartWith("!");
            entry.Consequences.ShouldNotStartWith("!");
            entry.WhatHappensNext.ShouldNotStartWith("!");
        }
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
