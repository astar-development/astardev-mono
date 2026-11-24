using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The default and the accepted values of every setting in <see cref="SettingNames"/>.</summary>
internal static class SettingDefinitions
{
    private static readonly string[] Themes = ["Dark", "Light", "Hacker", "Purple Unicorn", "System"];

    private static readonly string[] Booleans = ["true", "false"];

    private static readonly Dictionary<string, (string Default, Func<string, bool> IsValid)> All = new()
    {
        [SettingNames.Theme] = ("Dark", Themes.Contains),
        [SettingNames.NotifyOnConflict] = ("true", Booleans.Contains),
        [SettingNames.NotifyOnError] = ("true", Booleans.Contains),
        [SettingNames.NotifyOnCompletion] = ("false", Booleans.Contains),
        [SettingNames.AuditRetentionDays] = ("90", IsPositiveWholeNumber),
        [SettingNames.TelemetryRetentionDays] = ("7", IsPositiveWholeNumber)
    };

    /// <summary>The default of every known setting, by name.</summary>
    public static IEnumerable<KeyValuePair<string, string>> Defaults => All.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Default));

    /// <summary>Whether <paramref name="name"/> is a known setting.</summary>
    /// <param name="name">The name to check.</param>
    /// <returns><see langword="true"/> for a name in <see cref="SettingNames"/>.</returns>
    public static bool IsKnown(string name) => All.ContainsKey(name);

    /// <summary>Whether <paramref name="value"/> fits the known setting <paramref name="name"/>.</summary>
    /// <param name="name">A name for which <see cref="IsKnown"/> is <see langword="true"/>.</param>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value is accepted for the setting.</returns>
    public static bool IsValid(string name, string value) => All[name].IsValid(value);

    private static bool IsPositiveWholeNumber(string value) => int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var days) && days >= 1;
}
