namespace AStarDev.CloudSync.UI;

/// <summary>Lets the user choose where to save a file.</summary>
public interface IFilePicker
{
    /// <summary>Asks the user for a file to save to.</summary>
    /// <param name="suggestedName">The file name the dialog offers, including its extension.</param>
    /// <param name="cancellationToken">Cancelling abandons the dialog.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user dismissed the dialog.</returns>
    Task<string?> PickSaveFileAsync(string suggestedName, CancellationToken cancellationToken);
}
