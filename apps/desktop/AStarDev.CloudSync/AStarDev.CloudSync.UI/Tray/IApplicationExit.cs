namespace AStarDev.CloudSync.UI.Tray;

/// <summary>Ends the UI process. The daemon is a separate process and is not stopped.</summary>
internal interface IApplicationExit
{
    /// <summary>Shuts the application down.</summary>
    void Exit();
}
