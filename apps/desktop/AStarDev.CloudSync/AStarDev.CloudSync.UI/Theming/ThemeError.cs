namespace AStarDev.CloudSync.UI.Theming;

/// <summary>No Avalonia application is running, so there is nothing to apply a theme to.</summary>
internal sealed record NoApplication;

/// <summary>No resource dictionary exists for the requested theme.</summary>
/// <param name="Theme">The theme name asked for.</param>
internal sealed record UnknownTheme(string Theme);

/// <summary>Everything that can go wrong applying a theme, as values. Match it exhaustively.</summary>
internal union ThemeError(NoApplication, UnknownTheme);
