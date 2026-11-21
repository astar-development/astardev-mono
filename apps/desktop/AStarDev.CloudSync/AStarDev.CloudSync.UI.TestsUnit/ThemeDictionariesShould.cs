using System.Xml.Linq;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ThemeDictionariesShould
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] TrustLevels = ["Healthy", "Active", "Degraded", "Attention", "Critical"];

    private static string ThemesRoot => Path.Combine(AppContext.BaseDirectory, "Themes");

    private static IEnumerable<string> ThemeFolders => Directory.GetDirectories(ThemesRoot).Where(folder => Path.GetFileName(folder) != "Shared");

    private static HashSet<string> KeysIn(string path) => XDocument.Load(path).Root!.Elements().Select(element => (string?)element.Attribute(Xaml + "Key")).OfType<string>().ToHashSet();

    private static HashSet<string> ThemeKeys(string folder) => Directory.GetFiles(folder, "*.axaml").SelectMany(KeysIn).ToHashSet();

    [Fact]
    public void define_the_dark_theme_with_every_trust_colour_surface_and_text()
    {
        var keys = ThemeKeys(Path.Combine(ThemesRoot, "Dark"));

        foreach (var level in TrustLevels) keys.ShouldContain($"Trust.{level}.Brush");
        keys.ShouldContain("Surface.Brush");
        keys.ShouldContain("Text.Primary.Brush");
    }

    [Fact]
    public void give_every_theme_the_same_keys_as_dark_so_a_new_theme_is_only_a_folder()
    {
        var dark = ThemeKeys(Path.Combine(ThemesRoot, "Dark"));

        foreach (var folder in ThemeFolders) ThemeKeys(folder).ShouldBe(dark, true, Path.GetFileName(folder));
    }

    [Fact]
    public void prefix_the_keys_of_a_feature_file_with_its_feature_name()
    {
        foreach (var path in ThemeFolders.SelectMany(folder => Directory.GetFiles(folder, "*.axaml")))
        {
            var feature = Path.GetFileNameWithoutExtension(path);
            if (feature == "Common") continue;

            foreach (var key in KeysIn(path)) key.StartsWith($"{feature}.", StringComparison.Ordinal).ShouldBeTrue($"{path}: {key}");
        }
    }

    [Fact]
    public void define_one_icon_per_trust_level_outside_any_theme()
    {
        var keys = KeysIn(Path.Combine(ThemesRoot, "Shared", "Icons.axaml"));

        foreach (var level in TrustLevels) keys.ShouldContain($"Icon.Trust.{level}");
    }
}
