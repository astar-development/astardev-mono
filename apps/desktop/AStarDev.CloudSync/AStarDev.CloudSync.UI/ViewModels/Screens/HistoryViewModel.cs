using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Operations;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The History screen: what happened. Shows sample completed and failed operations, plus a placeholder Audit tab.</summary>
internal sealed class HistoryViewModel : OperationListViewModel, IScreen
{
    private readonly ILocalisationService localisation;
    private int tabIndex;

    /// <summary>Creates the screen on the History tab with its filters and sample operations.</summary>
    public HistoryViewModel(ILocalisationService localisation, ICultureService culture)
        : base(localisation, culture, [OperationStatus.Completed, OperationStatus.Failed], true) => this.localisation = localisation;

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.History;

    /// <inheritdoc/>
    public string Title => localisation.Get("History.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("History.Glyph");

    /// <summary>Index of the selected tab: 0 is History, 1 is the Audit placeholder.</summary>
    public int TabIndex
    {
        get => tabIndex;
        set => SetProperty(ref tabIndex, value);
    }

    /// <summary>Text of the History tab.</summary>
    public string HistoryTabText => localisation.Get("History.Tab.History");

    /// <summary>Text of the Audit tab.</summary>
    public string AuditTabText => localisation.Get("History.Tab.Audit");

    /// <summary>Text saying the Audit tab is a placeholder: doc 19 has no Audit destination, so its place is pending confirmation.</summary>
    public string AuditPendingText => localisation.Get("History.Audit.Pending");
}
