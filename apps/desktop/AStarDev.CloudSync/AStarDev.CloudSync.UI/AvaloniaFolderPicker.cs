using AStarDev.CloudSync.UI.Tray;
using Avalonia.Platform.Storage;

namespace AStarDev.CloudSync.UI;

/// <summary>Shows the platform's folder dialog, owned by the main window.</summary>
/// <param name="shell">Supplies the main window the dialog opens over.</param>
internal sealed class AvaloniaFolderPicker(AvaloniaShell shell) : IFolderPicker
{
    /// <inheritdoc/>
    /// <remarks>Also returns <see langword="null"/> when the main window is not attached yet, or when the chosen folder has no local path, which does not happen for local disks.</remarks>
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        if (shell.Window is not { } owner) return null;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = ApplicationConstants.PickFolderTitle, AllowMultiple = false }).WaitAsync(cancellationToken);

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}
