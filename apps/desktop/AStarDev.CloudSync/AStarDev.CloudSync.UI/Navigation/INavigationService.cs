using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Navigation;

/// <summary>Decides which screen the content area shows. Cards, the tray and notifications route through it.</summary>
public interface INavigationService
{
    /// <summary>The screen being shown. The Dashboard until something navigates.</summary>
    IScreen Current { get; }

    /// <summary>The deep-link target given with the last navigation, or <see langword="null"/> when there was none.</summary>
    string? CurrentTargetId { get; }

    /// <summary>Raised after every successful navigation, including one to the screen already shown.</summary>
    event EventHandler? Navigated;

    /// <summary>Shows <paramref name="screen"/> and tells it the target.</summary>
    /// <param name="screen">The destination.</param>
    /// <param name="targetId">An item to focus on the screen, such as a conflict id. Blank counts as none.</param>
    /// <returns>Success; or <see cref="ScreenNotRegistered"/>, leaving the current screen unchanged.</returns>
    Result<Unit, NavigationError> Navigate(ScreenId screen, string? targetId = null);
}
