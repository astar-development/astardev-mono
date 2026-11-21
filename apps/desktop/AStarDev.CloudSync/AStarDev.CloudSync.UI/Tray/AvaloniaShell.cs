using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace AStarDev.CloudSync.UI.Tray;

/// <summary>The real main window and desktop lifetime behind <see cref="IMainWindowHost"/> and <see cref="IApplicationExit"/>. Does nothing until <see cref="Attach"/> is called.</summary>
internal sealed class AvaloniaShell : IMainWindowHost, IApplicationExit
{
    private IClassicDesktopStyleApplicationLifetime? lifetime;

    /// <summary>The main window once attached, for dialogs that need an owner; <see langword="null"/> before.</summary>
    public Window? Window { get; private set; }

    /// <summary>Supplies the window and lifetime. Call once the desktop lifetime exists.</summary>
    public void Attach(Window window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        Window = window;
        lifetime = desktop;
    }

    /// <inheritdoc/>
    public void Show()
    {
        Window?.Show();
        Window?.Activate();
    }

    /// <inheritdoc/>
    public void Hide() => Window?.Hide();

    /// <inheritdoc/>
    public void Exit() => lifetime?.Shutdown();
}
