using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AStarDev.CloudSync.UI;

/// <summary>Shows the platform's folder dialog, owned by the window it is given.</summary>
/// <param name="owner">The window the dialog opens over.</param>
internal sealed class AvaloniaFolderPicker(TopLevel owner) : IFolderPicker
{
    /// <inheritdoc/>
    /// <remarks>Also returns <see langword="null"/> when the chosen folder has no local path, which does not happen for local disks.</remarks>
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = ApplicationConstants.PickFolderTitle, AllowMultiple = false }).WaitAsync(cancellationToken);

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}
