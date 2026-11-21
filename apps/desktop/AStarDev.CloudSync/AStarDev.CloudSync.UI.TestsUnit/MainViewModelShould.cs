using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class MainViewModelShould
{
    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();

        return builder.Build();
    }

    [Fact]
    public void expose_seven_primary_destinations()
    {
        using var host = CreateHost();

        var destinations = host.Services.GetRequiredService<MainViewModel>().Destinations;

        destinations.Select(destination => destination.Id).ShouldBe([ScreenId.Dashboard, ScreenId.Queue, ScreenId.Conflicts, ScreenId.Activity, ScreenId.History, ScreenId.Providers, ScreenId.Settings]);
        foreach (var destination in destinations)
        {
            destination.Title.ShouldNotStartWith("!");
            destination.Glyph.ShouldNotStartWith("!");
        }
    }

    [Fact]
    public void land_on_the_dashboard_with_its_rail_entry_selected()
    {
        using var host = CreateHost();

        var main = host.Services.GetRequiredService<MainViewModel>();

        main.Content.Id.ShouldBe(ScreenId.Dashboard);
        main.SelectedDestination!.Id.ShouldBe(ScreenId.Dashboard);
    }

    [Fact]
    public void navigate_when_a_rail_entry_is_selected()
    {
        using var host = CreateHost();
        var main = host.Services.GetRequiredService<MainViewModel>();

        main.SelectedDestination = main.Destinations.Single(destination => destination.Id == ScreenId.Queue);

        main.Content.Id.ShouldBe(ScreenId.Queue);
    }

    [Fact]
    public void follow_a_deep_link_and_keep_settings_selected_for_an_advanced_screen()
    {
        using var host = CreateHost();
        var main = host.Services.GetRequiredService<MainViewModel>();

        _ = host.Services.GetRequiredService<INavigationService>().Navigate(ScreenId.Traces);

        main.Content.Id.ShouldBe(ScreenId.Traces);
        main.SelectedDestination!.Id.ShouldBe(ScreenId.Settings);
    }
}
