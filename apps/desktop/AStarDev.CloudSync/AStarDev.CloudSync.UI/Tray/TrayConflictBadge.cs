using System.Collections.Specialized;
using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.UI.Tray;

/// <summary>
/// Keeps the tray in step with <see cref="ConnectionViewModel.Conflicts"/>: the count follows every conflict added or removed, and the icon reads <see cref="TrustLevel.Attention"/> while any wait
/// and <see cref="TrustLevel.Healthy"/> when none do. Constructing it starts the listening, so resolve it once at startup.
/// </summary>
internal sealed class TrayConflictBadge
{
    private readonly ConnectionViewModel connection;
    private readonly TrayLifecycle tray;

    /// <summary>Starts following the conflicts and applies the current count straight away.</summary>
    /// <param name="connection">Holds the conflicts that wait for the user.</param>
    /// <param name="tray">Receives the count and the icon state.</param>
    public TrayConflictBadge(ConnectionViewModel connection, TrayLifecycle tray)
    {
        this.connection = connection;
        this.tray = tray;
        connection.Conflicts.CollectionChanged += OnConflictsChanged;
        Apply();
    }

    private void OnConflictsChanged(object? sender, NotifyCollectionChangedEventArgs args) => Apply();

    private void Apply()
    {
        var count = connection.Conflicts.Count;
        tray.SetIconState(count > 0 ? TrustLevel.Attention : TrustLevel.Healthy);
        tray.SetConflictCount(count);
    }
}
