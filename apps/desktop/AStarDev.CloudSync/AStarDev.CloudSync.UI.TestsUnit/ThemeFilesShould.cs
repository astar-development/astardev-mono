using System.Reflection;
using AStarDev.CloudSync.UI.Theming;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ThemeFilesShould
{
    [Fact]
    public void list_every_dictionary_under_the_dark_and_shared_folders() => ThemeService.Files("Dark").Order().ShouldBe(OnDisk("Dark").Concat(OnDisk("Shared")).Order());

    private static IEnumerable<string> OnDisk(string folder) => Directory.GetFiles(Path.Combine(ThemesRoot(), folder), "*.axaml").Select(file => $"{folder}/{Path.GetFileNameWithoutExtension(file)}");

    private static string ThemesRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "") => Path.Combine(Path.GetDirectoryName(source)!, "..", "AStarDev.CloudSync.UI", "Themes");
}
