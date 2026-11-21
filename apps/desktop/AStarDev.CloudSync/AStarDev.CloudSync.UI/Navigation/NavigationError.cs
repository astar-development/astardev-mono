namespace AStarDev.CloudSync.UI.Navigation;

/// <summary>No <see cref="IScreen"/> is registered for the requested destination.</summary>
/// <param name="Screen">The destination asked for.</param>
public sealed record ScreenNotRegistered(ScreenId Screen);

/// <summary>Everything that can go wrong navigating, as values. Match it exhaustively.</summary>
public union NavigationError(ScreenNotRegistered);
