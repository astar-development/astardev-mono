namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>How the UI's link to the daemon is doing, as shown in the status banner.</summary>
public enum ConnectionState
{
    /// <summary>No link: before the first attempt, or waiting out the backoff after a failed one.</summary>
    Disconnected,

    /// <summary>A retry attempt is in progress after the first attempt or a lost link.</summary>
    Reconnecting,

    /// <summary>Connected, the initial status is loaded and the event subscription is open.</summary>
    Connected
}
