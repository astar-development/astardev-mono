using System.Collections.ObjectModel;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Operations;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The Activity screen: what is happening now. Shows what the daemon reports for each job, then the pending and running operations.</summary>
internal sealed class ActivityViewModel : OperationListViewModel, IScreen
{
    private readonly ILocalisationService localisation;

    /// <summary>Creates the screen with its filters and starts following the daemon's activities.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date, time and number.</param>
    /// <param name="connection">Holds the activities the daemon reported; the screen rebuilds its rows whenever they change.</param>
    public ActivityViewModel(ILocalisationService localisation, ICultureService culture, ConnectionViewModel connection)
        : base(localisation, culture, [OperationStatus.Pending, OperationStatus.Running], true)
    {
        this.localisation = localisation;
        void Rebuild()
        {
            DaemonActivities.Clear();
            foreach (var activity in connection.Activities) DaemonActivities.Add(new DaemonActivityRowViewModel(activity, localisation));
            OnPropertyChanged(nameof(HasDaemonActivities));
        }

        connection.Activities.CollectionChanged += (_, _) => Rebuild();
        Rebuild();
    }

    /// <summary>What the daemon is doing for each job, in the daemon's own numbers.</summary>
    public ObservableCollection<DaemonActivityRowViewModel> DaemonActivities { get; } = [];

    /// <summary>Whether the daemon has reported anything, so the section can hide itself when idle.</summary>
    public bool HasDaemonActivities => DaemonActivities.Count > 0;

    /// <summary>Heading of the daemon activity section.</summary>
    public string DaemonActivitiesHeading => localisation.Get("Activity.Daemon.Heading");

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Activity;

    /// <inheritdoc/>
    public string Title => localisation.Get("Activity.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Activity.Glyph");
}
