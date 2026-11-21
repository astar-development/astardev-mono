using AStarDev.CloudSync.Ports;
using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>MSAL public client signing in through the system browser with a loopback redirect.</summary>
internal sealed class MsalBrowserSignIn(OneDriveOptions options) : IBrowserSignIn
{
    private static readonly string[] Scopes = ["Files.ReadWrite", "offline_access"];

    /// <inheritdoc/>
    public async Task<SignInOutcome> SignInAsync(byte[] cache, CancellationToken cancellationToken)
    {
        var application = MsalApplication.Create(options, cache);
        var result = await application.Client.AcquireTokenInteractive(Scopes).ExecuteAsync(cancellationToken).ConfigureAwait(false);

        return new SignInOutcome(new ProviderAccount(result.Account.HomeAccountId.Identifier, result.Account.Username), application.Cache, result.AccessToken);
    }
}
