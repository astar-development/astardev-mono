namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>State behind <see cref="Views.MainWindow"/>.</summary>
/// <param name="connection">The app-wide daemon connection, shared rather than created here so its retry loop outlives any one view.</param>
public sealed class MainViewModel(ConnectionViewModel connection) : ViewModelBase
{
    /// <summary>Daemon link state for the status banner.</summary>
    public ConnectionViewModel Connection { get; } = connection;
}
