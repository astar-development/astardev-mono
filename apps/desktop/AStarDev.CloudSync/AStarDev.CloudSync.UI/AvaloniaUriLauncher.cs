using AStarDev.CloudSync.UI.Tray;
using Avalonia.Controls;

namespace AStarDev.CloudSync.UI;

/// <summary>Opens addresses through the main window's platform launcher.</summary>
/// <param name="shell">Supplies the main window that owns the launcher.</param>
internal sealed class AvaloniaUriLauncher(AvaloniaShell shell) : IUriLauncher
{
    /// <inheritdoc/>
    public async Task<bool> LaunchAsync(Uri uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return TopLevel.GetTopLevel(shell.Window) is { } top && await top.Launcher.LaunchUriAsync(uri).WaitAsync(cancellationToken);
    }
}
