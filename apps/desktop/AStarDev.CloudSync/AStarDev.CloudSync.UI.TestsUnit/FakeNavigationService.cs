using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeNavigationService : INavigationService
{
    public List<ScreenId> Visited { get; } = [];

    public IScreen Current => throw new NotSupportedException();

    public string? CurrentTargetId => null;

    public event EventHandler? Navigated
    {
        add { }
        remove { }
    }

    public Result<Unit, NavigationError> Navigate(ScreenId screen, string? targetId = null)
    {
        Visited.Add(screen);

        return new Ok<Unit>(default);
    }
}
