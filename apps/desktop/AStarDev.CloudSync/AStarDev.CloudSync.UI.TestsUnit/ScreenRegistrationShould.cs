using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ScreenRegistrationShould
{
    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();

        return builder.Build();
    }

    [Fact]
    public void register_exactly_one_screen_per_screen_id()
    {
        using var host = CreateHost();

        var ids = host.Services.GetServices<IScreen>().Select(screen => screen.Id).Order().ToList();

        ids.ShouldBe(Enum.GetValues<ScreenId>().Order().ToList());
    }

    [Fact]
    public void give_every_screen_a_localised_title_and_glyph()
    {
        using var host = CreateHost();

        foreach (var screen in host.Services.GetServices<IScreen>())
        {
            screen.Title.ShouldNotStartWith("!");
            screen.Glyph.ShouldNotStartWith("!");
        }
    }

    [Fact]
    public void show_a_localised_not_available_text_on_a_stub()
    {
        using var host = CreateHost();

        var stub = host.Services.GetServices<IScreen>().OfType<StubScreenViewModel>().First();

        stub.NotAvailableText.ShouldNotStartWith("!");
    }

    [Fact]
    public void share_one_navigation_service_with_the_main_view_model()
    {
        using var host = CreateHost();

        host.Services.GetRequiredService<INavigationService>().ShouldBeSameAs(host.Services.GetRequiredService<INavigationService>());
    }

    [Fact]
    public void show_status_bar_text_from_localisation()
    {
        using var host = CreateHost();

        var bar = host.Services.GetRequiredService<StatusBarViewModel>();

        bar.ProviderHealth.Text.ShouldNotStartWith("!");
        bar.QueueActivityText.ShouldNotStartWith("!");
        bar.ErrorStateText.ShouldNotStartWith("!");
        bar.LastActivityText.ShouldNotStartWith("!");
    }
}
