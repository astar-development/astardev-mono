using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>
/// Base of a placeholder screen: shows its localised title and a "not yet available" text. The title and glyph come from the
/// <c>&lt;ScreenId&gt;.Title</c> and <c>&lt;ScreenId&gt;.Glyph</c> keys of the screen's own strings file. A slice replaces its stub by changing the screen's view model to
/// derive from <see cref="ViewModelBase"/> and implement <see cref="IScreen"/> directly, and by adding <c>Views/Screens/&lt;Name&gt;View</c>.
/// </summary>
internal abstract class StubScreenViewModel(ScreenId id, ILocalisationService localisation) : ViewModelBase, IScreen
{
    /// <inheritdoc/>
    public ScreenId Id => id;

    /// <inheritdoc/>
    public string Title => localisation.Get($"{id}.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get($"{id}.Glyph");

    /// <summary>Localised text saying the screen is not built yet.</summary>
    public string NotAvailableText => localisation.Get("Navigation.NotAvailable");
}
