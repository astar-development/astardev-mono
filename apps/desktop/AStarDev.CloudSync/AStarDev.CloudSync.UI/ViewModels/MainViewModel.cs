namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>State behind <see cref="Views.MainWindow"/>.</summary>
/// <param name="connection">The app-wide daemon connection, shared rather than created here so its retry loop outlives any one view.</param>
/// <param name="connect">The account connection, kept here so its result survives the view being rebuilt.</param>
public sealed class MainViewModel(ConnectionViewModel connection, ConnectViewModel connect) : ViewModelBase
{
    /// <summary>Daemon link state for the status banner.</summary>
    public ConnectionViewModel Connection { get; } = connection;

    /// <summary>Connect button, busy state, item count and errors.</summary>
    public ConnectViewModel Connect { get; } = connect;
}
