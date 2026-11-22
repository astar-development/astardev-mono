using AStarDev.CloudSync.UI.ViewModels.Providers;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ProvidersUiShould
{
    [Fact]
    public void reference_no_provider_implementation_assembly()
    {
        var references = typeof(ProviderDescriptor).Assembly.GetReferencedAssemblies().Select(name => name.Name);

        references.ShouldNotContain(name => name!.Contains("OneDrive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void keep_provider_specific_names_out_of_the_provider_view_models_and_views()
    {
        var types = typeof(ProviderDescriptor).Assembly.GetTypes().Where(type => type.Namespace is "AStarDev.CloudSync.UI.ViewModels.Providers" or "AStarDev.CloudSync.UI.Views.Providers");

        types.ShouldNotContain(type => type.Name.Contains("OneDrive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void define_a_theme_colour_and_a_shared_icon_for_every_health_state()
    {
        var themes = Path.Combine(AppContext.BaseDirectory, "Themes");
        var dark = File.ReadAllText(Path.Combine(themes, "Dark", "Providers.axaml"));
        var icons = File.ReadAllText(Path.Combine(themes, "Shared", "ProvidersIcons.axaml"));

        foreach (var health in Enum.GetValues<ProviderHealth>())
        {
            dark.ShouldContain($"Providers.Health.{health}.Brush");
            icons.ShouldContain($"Icon.Providers.Health.{health}");
        }
    }
}
