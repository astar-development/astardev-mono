namespace AStarDev.CloudSync.UI.Tray;

/// <summary>The main window as the tray sees it, so the lifecycle rules run without a display.</summary>
internal interface IMainWindowHost
{
    /// <summary>Shows the window and brings it to the front. Safe to call while it is already visible.</summary>
    void Show();

    /// <summary>Hides the window without closing it. Safe to call while it is already hidden.</summary>
    void Hide();
}
