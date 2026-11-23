using AStarDev.CloudSync.UI.Tray;
using Avalonia.Platform.Storage;

namespace AStarDev.CloudSync.UI;

/// <summary>Shows the platform's save dialog, owned by the main window.</summary>
/// <param name="shell">Supplies the main window the dialog opens over.</param>
internal sealed class AvaloniaFilePicker(AvaloniaShell shell) : IFilePicker
{
    /// <inheritdoc/>
    /// <remarks>Also returns <see langword="null"/> when the main window is not attached yet, or when the chosen file has no local path, which does not happen for local disks.</remarks>
    public async Task<string?> PickSaveFileAsync(string suggestedName, CancellationToken cancellationToken)
    {
        if (shell.Window is not { } owner) return null;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = ApplicationConstants.SaveFileTitle, SuggestedFileName = suggestedName, DefaultExtension = ApplicationConstants.CsvExtension }).WaitAsync(cancellationToken);

        return file?.TryGetLocalPath();
    }
}
