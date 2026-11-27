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
    public void expose_nine_primary_destinations()
    {
        using var host = CreateHost();

        var destinations = host.Services.GetRequiredService<MainViewModel>().Destinations;

        destinations.Select(destination => destination.Id).ShouldBe([ScreenId.Dashboard, ScreenId.Queue, ScreenId.Conflicts, ScreenId.Activity, ScreenId.History, ScreenId.Audit, ScreenId.Providers, ScreenId.Classifications, ScreenId.Settings]);
        foreach (var destination in destinations)
        {
            destination.Title.ShouldNotStartWith("!");
            destination.Glyph.ShouldNotStartWith("!");
        }
    }

    [Fact]
    public void show_the_starting_up_screen_until_the_daemon_has_connected()
    {
        using var host = CreateHost();
        var main = host.Services.GetRequiredService<MainViewModel>();
        main.IsStarting.ShouldBeTrue();
        main.IsStartupFailed.ShouldBeFalse();

        host.Services.GetRequiredService<ConnectionViewModel>().HasConnected = true;

        main.IsStarting.ShouldBeFalse();
    }

    [Fact]
    public void say_the_daemon_cannot_be_reached_once_startup_has_timed_out()
    {
        using var host = CreateHost();
        var main = host.Services.GetRequiredService<MainViewModel>();

        host.Services.GetRequiredService<ConnectionViewModel>().StartupTimedOut = true;

        main.IsStartupFailed.ShouldBeTrue();
        main.IsStartupWaiting.ShouldBeFalse();
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

    [Fact]
    public void offer_a_way_back_to_settings_from_an_advanced_screen_only()
    {
        using var host = CreateHost();
        var main = host.Services.GetRequiredService<MainViewModel>();
        var navigation = host.Services.GetRequiredService<INavigationService>();

        main.IsAdvancedScreen.ShouldBeFalse();
        _ = navigation.Navigate(ScreenId.Metrics);
        main.IsAdvancedScreen.ShouldBeTrue();
        main.BackCommand.Execute(null);

        main.Content.Id.ShouldBe(ScreenId.Settings);
        main.IsAdvancedScreen.ShouldBeFalse();
    }
}
