using AStarDev.CloudSync.Ports;
using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Maps exceptions captured around MSAL onto <see cref="ProviderError"/>, so no MSAL type crosses the provider boundary.</summary>
internal static class AuthenticationErrors
{
    /// <summary>
    /// Classifies a sign-in or keyring failure: MSAL errors are <see cref="AuthenticationFailed"/> except an unavailable identity service,
    /// which with network and I/O errors is <see cref="Transient"/> and worth a retry, anything else is <see cref="Permanent"/>.
    /// </summary>
    public static ProviderError From(Exception exception) => exception switch
    {
        MsalServiceException { ErrorCode: MsalError.ServiceNotAvailable } or MsalServiceException { StatusCode: >= 500 } => new Transient(exception.Message),
        MsalException => new AuthenticationFailed(exception.Message),
        HttpRequestException or IOException => new Transient(exception.Message),
        _ => new Permanent(exception.Message)
    };
}
