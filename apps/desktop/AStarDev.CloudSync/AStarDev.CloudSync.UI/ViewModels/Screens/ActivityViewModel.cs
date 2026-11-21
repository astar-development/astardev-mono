using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Operations;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The Activity screen: what is happening now. Shows sample pending and running operations until the daemon reports real ones.</summary>
internal sealed class ActivityViewModel : OperationListViewModel, IScreen
{
    private readonly ILocalisationService localisation;

    /// <summary>Creates the screen with its filters and sample operations.</summary>
    public ActivityViewModel(ILocalisationService localisation, ICultureService culture)
        : base(localisation, culture, [OperationStatus.Pending, OperationStatus.Running], true) => this.localisation = localisation;

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Activity;

    /// <inheritdoc/>
    public string Title => localisation.Get("Activity.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Activity.Glyph");
}
