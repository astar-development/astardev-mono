using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Navigation;

/// <summary>Keeps the registered screens by id and which one is current.</summary>
internal sealed class NavigationService : INavigationService
{
    private readonly Dictionary<ScreenId, IScreen> screens;

    /// <summary>Indexes the screens and lands on the Dashboard.</summary>
    /// <param name="registered">Every registered screen. Must include a Dashboard and no duplicate ids; composition tests guard this.</param>
    public NavigationService(IEnumerable<IScreen> registered)
    {
        screens = registered.ToDictionary(screen => screen.Id);
        Current = screens[ScreenId.Dashboard];
    }

    /// <inheritdoc/>
    public event EventHandler? Navigated;

    /// <inheritdoc/>
    public IScreen Current { get; private set; }

    /// <inheritdoc/>
    public string? CurrentTargetId { get; private set; }

    /// <inheritdoc/>
    public Result<Unit, NavigationError> Navigate(ScreenId screen, string? targetId = null)
    {
        if (!screens.TryGetValue(screen, out var destination)) return new Fail<NavigationError>(new ScreenNotRegistered(screen));

        Current = destination;
        CurrentTargetId = string.IsNullOrWhiteSpace(targetId) ? null : targetId;
        destination.OnNavigatedTo(CurrentTargetId);
        Navigated?.Invoke(this, EventArgs.Empty);

        return new Ok<Unit>(default);
    }
}
