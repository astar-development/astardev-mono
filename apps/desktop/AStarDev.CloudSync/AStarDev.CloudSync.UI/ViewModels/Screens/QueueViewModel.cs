using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Operations;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The Queue screen: operation visibility across pending, running, completed and failed work, as sample data. It never resolves conflicts.</summary>
internal sealed class QueueViewModel : OperationListViewModel, IScreen
{
    private readonly ILocalisationService localisation;

    /// <summary>Creates the screen with every status visible and no filters.</summary>
    public QueueViewModel(ILocalisationService localisation, ICultureService culture)
        : base(localisation, culture, Enum.GetValues<OperationStatus>(), false)
    {
        this.localisation = localisation;
        StatusCounts = Rows.GroupBy(row => row.Entry.Status).OrderBy(group => group.Key).Select(group => new StatusIndicatorViewModel(OperationStatusLevel.Of(group.Key), $"{localisation.Get($"Operations.Status.{group.Key}")}: {culture.FormatNumber(group.Count())}")).ToList();
        ConflictsNote = localisation.Get("Queue.ConflictsNote");
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Queue;

    /// <inheritdoc/>
    public string Title => localisation.Get("Queue.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Queue.Glyph");

    /// <summary>How many operations are in each status, as colour, icon and text. Statuses with none are left out.</summary>
    public IReadOnlyList<StatusIndicatorViewModel> StatusCounts { get; }

    /// <summary>Text pointing the user to the Conflicts screen, the only place conflicts are resolved.</summary>
    public string ConflictsNote { get; }
}
