using System.ComponentModel;
using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.UI.Tray;

/// <summary>
/// Keeps the tray in step with <see cref="ConnectionViewModel.ConflictsTotal"/>: the count follows every conflict detected or resolved, loaded or not, and the icon reads <see cref="TrustLevel.Attention"/> while any wait
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
        connection.PropertyChanged += OnConnectionChanged;
        Apply();
    }

    private void OnConnectionChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ConnectionViewModel.ConflictsTotal)) Apply();
    }

    private void Apply()
    {
        var count = connection.ConflictsTotal;
        tray.SetIconState(count > 0 ? TrustLevel.Attention : TrustLevel.Healthy);
        tray.SetConflictCount(count);
    }
}
