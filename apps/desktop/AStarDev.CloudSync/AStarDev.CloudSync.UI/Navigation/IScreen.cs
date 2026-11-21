namespace AStarDev.CloudSync.UI.Navigation;

/// <summary>
/// One destination. A class implementing it (normally a view model deriving from <c>ViewModelBase</c>) is found by assembly scan and registered as a singleton by
/// <c>AddCloudSyncUi</c>, so adding or replacing a screen touches only that screen's files. Its view is <c>Views/Screens/&lt;Name&gt;View</c>, found by <c>ViewLocator</c>.
/// </summary>
public interface IScreen
{
    /// <summary>The destination this screen serves. Exactly one registered screen per <see cref="ScreenId"/>.</summary>
    ScreenId Id { get; }

    /// <summary>Localised name, shown as the page title and the rail tooltip.</summary>
    string Title { get; }

    /// <summary>Localised short symbol drawn on the rail.</summary>
    string Glyph { get; }

    /// <summary>Called each time the screen is navigated to. The default ignores it; a screen that can focus an item (a conflict, an operation) overrides it.</summary>
    /// <param name="targetId">The deep-link target, or <see langword="null"/> when none was given.</param>
    void OnNavigatedTo(string? targetId)
    {
    }
}
