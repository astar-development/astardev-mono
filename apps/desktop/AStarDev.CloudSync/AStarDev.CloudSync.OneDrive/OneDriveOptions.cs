using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>The settings OneDrive sign-in needs: the app registration's client id and the personal-accounts authority.</summary>
internal sealed record OneDriveOptions(string ClientId, string Authority)
{
    public const string ConsumersAuthority = "https://login.microsoftonline.com/consumers";

    public static Result<OneDriveOptions, ProviderError> Create(string? clientId)
        => string.IsNullOrWhiteSpace(clientId)
            ? new Fail<ProviderError>(new AuthenticationFailed("The OneDrive client id is not configured."))
            : new Ok<OneDriveOptions>(new OneDriveOptions(clientId, ConsumersAuthority));
}
