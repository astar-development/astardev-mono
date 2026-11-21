namespace AStarDev.CloudSync.UI;

/// <summary>Lets the user choose a local folder.</summary>
public interface IFolderPicker
{
    /// <summary>Asks the user for a folder.</summary>
    /// <param name="cancellationToken">Cancelling abandons the dialog.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user dismissed the dialog.</returns>
    Task<string?> PickFolderAsync(CancellationToken cancellationToken);
}
