using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Turns provider failures into text the UI can show to a person as it is.</summary>
internal static class ProviderErrorMessages
{
    /// <summary>
    /// One sentence per <see cref="ProviderError"/> case. An <see cref="AuthenticationFailed"/> message is passed through unchanged, because the provider
    /// already words it for the user (for example, that the keyring is unavailable). Other cases say what happened and whether trying again can help.
    /// </summary>
    public static string Describe(ProviderError error) => error switch
    {
        RateLimited e => $"The provider asked us to slow down. Try again in {Math.Ceiling(e.RetryAfter.TotalSeconds)} seconds.",
        Transient e => $"The provider could not be reached ({e.Message}). Check the connection and try again.",
        AuthenticationFailed e => e.Message,
        Permanent e => $"The provider refused the request: {e.Message}",
        InvalidDeltaToken => "The provider no longer accepts the saved change bookmark. The folder has to be listed again."
    };
}
