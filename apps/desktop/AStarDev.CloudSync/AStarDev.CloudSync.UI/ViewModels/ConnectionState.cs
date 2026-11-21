namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>How the UI's link to the daemon is doing, as shown in the status banner.</summary>
public enum ConnectionState
{
    Disconnected,
    Reconnecting,
    Connected
}
