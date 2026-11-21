using AStarDev.CloudSync.Ports;
using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Maps exceptions captured around MSAL onto <see cref="ProviderError"/>, so no MSAL type crosses the provider boundary.</summary>
internal static class AuthenticationErrors
{
    public static ProviderError From(Exception exception) => exception switch
    {
        MsalException => new AuthenticationFailed(exception.Message),
        HttpRequestException or IOException => new Transient(exception.Message),
        _ => new Permanent(exception.Message)
    };
}
