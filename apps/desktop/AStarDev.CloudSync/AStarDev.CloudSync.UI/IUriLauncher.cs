namespace AStarDev.CloudSync.UI;

/// <summary>Opens a web address in the user's browser.</summary>
internal interface IUriLauncher
{
    /// <summary>Asks the platform to open <paramref name="uri"/> in the default browser.</summary>
    /// <param name="uri">The absolute address to open.</param>
    /// <param name="cancellationToken">Cancelling abandons the request.</param>
    /// <returns><see langword="true"/> when the platform accepted the address; <see langword="false"/> when no browser could be started or the main window is not attached yet.</returns>
    Task<bool> LaunchAsync(Uri uri, CancellationToken cancellationToken);
}
