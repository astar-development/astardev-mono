using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Theming;

/// <summary>Merges the resource dictionaries found under <c>Themes/</c> into the running application.</summary>
internal sealed class ThemeService : IThemeService
{
    private const string Root = "avares://AStarDev.CloudSync.UI/Themes/";

    private readonly List<ResourceInclude> applied = [];

    /// <inheritdoc/>
    public string? Current { get; private set; }

    /// <inheritdoc/>
    public Result<Unit, ThemeError> Apply(string themeName)
    {
        if (Application.Current is not { } application) return new Fail<ThemeError>(new NoApplication());

        var themeFiles = AssetLoader.GetAssets(new Uri($"{Root}{themeName}/"), null).ToList();
        if (themeFiles.Count == 0) return new Fail<ThemeError>(new UnknownTheme(themeName));

        var includes = themeFiles.Concat(AssetLoader.GetAssets(new Uri($"{Root}Shared/"), null)).Select(file => new ResourceInclude(new Uri(Root)) { Source = file }).ToList();
        foreach (var previous in applied) application.Resources.MergedDictionaries.Remove(previous);
        applied.Clear();
        foreach (var include in includes) application.Resources.MergedDictionaries.Add(include);
        applied.AddRange(includes);
        Current = themeName;

        return new Ok<Unit>(default);
    }
}
