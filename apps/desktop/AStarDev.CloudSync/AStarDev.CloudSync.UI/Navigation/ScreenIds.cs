namespace AStarDev.CloudSync.UI.Navigation;

/// <summary>Which <see cref="ScreenId"/> values appear on the navigation rail.</summary>
public static class ScreenIds
{
    /// <summary>The rail destinations, in display order.</summary>
    public static IReadOnlyList<ScreenId> Primary { get; } =
    [
        ScreenId.Dashboard,
        ScreenId.Queue,
        ScreenId.Conflicts,
        ScreenId.Activity,
        ScreenId.History,
        ScreenId.Audit,
        ScreenId.Providers,
        ScreenId.Settings
    ];

    /// <summary>The rail entry that is highlighted while <paramref name="screen"/> is shown: the screen itself, or Settings for the advanced screens.</summary>
    public static ScreenId RailEntryOf(ScreenId screen) => Primary.Contains(screen) ? screen : ScreenId.Settings;
}
