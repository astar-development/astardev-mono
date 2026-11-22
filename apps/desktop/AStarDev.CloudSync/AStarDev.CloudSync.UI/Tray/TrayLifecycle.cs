using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Tray;

/// <summary>
/// The rules of the tray: closing the window hides it, and only <see cref="Exit"/> ends the UI. Slice U7 drives <see cref="SetIconState"/> for icon states;
/// notifications call <see cref="Open"/> to deep link.
/// </summary>
/// <param name="window">The main window.</param>
/// <param name="navigation">Where tray commands route.</param>
/// <param name="application">How the UI process ends.</param>
internal sealed class TrayLifecycle(IMainWindowHost window, INavigationService navigation, IApplicationExit application)
{
    private bool exiting;

    /// <summary>Raised after <see cref="IconState"/> or <see cref="ConflictCount"/> changes. The tray icon listens to redraw itself.</summary>
    public event EventHandler? IconStateChanged;

    /// <summary>What the tray icon should convey. Healthy until <see cref="SetIconState"/> says otherwise.</summary>
    public TrustLevel IconState { get; private set; } = TrustLevel.Healthy;

    /// <summary>How many conflicts wait for the user. Zero until <see cref="SetConflictCount"/> says otherwise; the tray shows a badge and a count while it is above zero.</summary>
    public int ConflictCount { get; private set; }

    /// <summary>Call when the main window is closing.</summary>
    /// <returns><see langword="true"/> when the close must be cancelled because the window was hidden to the tray instead; <see langword="false"/> once <see cref="Exit"/> was called, so the close goes ahead.</returns>
    public bool HandleWindowClosing()
    {
        if (exiting) return false;

        window.Hide();

        return true;
    }

    /// <summary>Shows the window and navigates to <paramref name="screen"/>. The entry point for tray items and notifications.</summary>
    /// <param name="screen">The destination.</param>
    /// <param name="targetId">An item to focus on the screen, or <see langword="null"/>.</param>
    /// <returns>The navigation result. The window is shown even when navigation fails.</returns>
    public Result<Unit, NavigationError> Open(ScreenId screen, string? targetId = null)
    {
        window.Show();

        return navigation.Navigate(screen, targetId);
    }

    /// <summary>Tray menu: Open Dashboard.</summary>
    public void OpenDashboard() => Open(ScreenId.Dashboard);

    /// <summary>Tray menu: Settings.</summary>
    public void OpenSettings() => Open(ScreenId.Settings);

    /// <summary>Tray menu: Conflicts. Opens the Conflict Centre.</summary>
    public void OpenConflicts() => Open(ScreenId.Conflicts);

    /// <summary>Tray menu: Pause Sync. Does nothing until the daemon has a pause command.</summary>
    public void PauseSync()
    {
    }

    /// <summary>Tray menu: Resume Sync. Does nothing until the daemon has a resume command.</summary>
    public void ResumeSync()
    {
    }

    /// <summary>Tray menu: Exit CloudSync. The only way the UI ends; later window closes are no longer cancelled.</summary>
    public void Exit()
    {
        exiting = true;
        application.Exit();
    }

    /// <summary>Sets the conflict count and raises <see cref="IconStateChanged"/>. A negative count is treated as zero.</summary>
    /// <param name="count">How many conflicts wait for the user.</param>
    public void SetConflictCount(int count)
    {
        ConflictCount = Math.Max(count, 0);
        IconStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Changes the icon state and raises <see cref="IconStateChanged"/>.</summary>
    /// <param name="state">The level the icon should convey.</param>
    public void SetIconState(TrustLevel state)
    {
        IconState = state;
        IconStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
