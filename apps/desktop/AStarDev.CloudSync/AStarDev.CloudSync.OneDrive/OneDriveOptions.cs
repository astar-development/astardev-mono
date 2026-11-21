using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>The settings OneDrive sign-in needs: the app registration's client id and the personal-accounts authority.</summary>
internal sealed record OneDriveOptions(string ClientId, string Authority)
{
    /// <summary>Authority for personal Microsoft accounts only. Work and school accounts cannot sign in through it.</summary>
    public const string ConsumersAuthority = "https://login.microsoftonline.com/consumers";

    /// <summary>Checks that a client id is configured and pairs it with <see cref="ConsumersAuthority"/>.</summary>
    /// <returns><see cref="AuthenticationFailed"/> when <paramref name="clientId"/> is missing or blank.</returns>
    public static Result<OneDriveOptions, ProviderError> Create(string? clientId)
        => string.IsNullOrWhiteSpace(clientId)
            ? new Fail<ProviderError>(new AuthenticationFailed("The OneDrive client id is not configured."))
            : new Ok<OneDriveOptions>(new OneDriveOptions(clientId, ConsumersAuthority));
}
