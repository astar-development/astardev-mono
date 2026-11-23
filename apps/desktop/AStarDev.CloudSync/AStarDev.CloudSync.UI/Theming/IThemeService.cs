using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Theming;

/// <summary>Makes one theme's resource dictionaries the application's colours. A theme is only a folder of dictionaries; no code knows theme names.</summary>
internal interface IThemeService
{
    /// <summary>The theme applied last, or <see langword="null"/> until <see cref="Apply"/> succeeds.</summary>
    public string? Current { get; }

    /// <summary>The names of the themes that have dictionaries, so a choice only offers what <see cref="Apply"/> accepts.</summary>
    public IReadOnlyList<string> Available { get; }

    /// <summary>
    /// Replaces the application's theme dictionaries with every <c>Themes/&lt;themeName&gt;/*.axaml</c> plus the theme-independent <c>Themes/Shared/*.axaml</c>.
    /// Controls bound with <c>DynamicResource</c> pick the change up. Safe to call again to switch themes.
    /// </summary>
    /// <returns><see cref="NoApplication"/> when Avalonia is not running; <see cref="UnknownTheme"/> when the theme has no dictionaries. The current theme is left untouched on failure.</returns>
    public Result<Unit, ThemeError> Apply(string themeName);
}
