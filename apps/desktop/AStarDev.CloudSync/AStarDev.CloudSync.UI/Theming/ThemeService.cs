using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Theming;

/// <summary>Merges the resource dictionaries of a theme into the running application.</summary>
internal sealed class ThemeService : IThemeService
{
    private const string Root = "avares://AStarDev.CloudSync.UI/Themes/";

    private static readonly string[] Shared = ["Icons", "ProvidersIcons"];

    private static readonly Dictionary<string, string[]> Themes = new() { ["Dark"] = ["Common", "Conflicts", "Navigation", "Onboarding", "Providers", "Settings"] };

    private readonly List<ResourceInclude> applied = [];

    /// <inheritdoc/>
    public string? Current { get; private set; }

    /// <inheritdoc/>
    public Result<Unit, ThemeError> Apply(string themeName)
    {
        if (Application.Current is not { } application) return new Fail<ThemeError>(new NoApplication());

        if (!Themes.ContainsKey(themeName)) return new Fail<ThemeError>(new UnknownTheme(themeName));

        var includes = Files(themeName).Select(Include).ToList();
        foreach (var previous in applied) application.Resources.MergedDictionaries.Remove(previous);
        applied.Clear();
        foreach (var include in includes) application.Resources.MergedDictionaries.Add(include);
        applied.AddRange(includes);
        Current = themeName;

        return new Ok<Unit>(default);
    }

    internal static IEnumerable<string> Files(string themeName) => Themes[themeName].Select(file => $"{themeName}/{file}").Concat(Shared.Select(file => $"Shared/{file}"));

    private static ResourceInclude Include(string path) => new(new Uri(Root)) { Source = new Uri($"{Root}{path}.axaml") };
}
