using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.UI.Navigation;

/// <summary>No <see cref="IScreen"/> is registered for the requested destination.</summary>
/// <param name="Screen">The destination asked for.</param>
public sealed record ScreenNotRegistered(ScreenId Screen) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => UiErrorCodes.ScreenNotRegistered;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>Everything that can go wrong navigating, as values. Match it exhaustively.</summary>
public union NavigationError(ScreenNotRegistered);
