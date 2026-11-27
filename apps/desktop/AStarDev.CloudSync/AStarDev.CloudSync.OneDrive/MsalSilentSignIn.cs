using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>MSAL public client acquiring tokens silently from the saved cache.</summary>
internal sealed class MsalSilentSignIn(OneDriveOptions options) : ISilentSignIn
{
    private const string AccountMissingMessage = "The account is not in the stored sign-in, so the user must sign in again.";

    private static readonly string[] Scopes = ["Files.ReadWrite", "offline_access"];

    /// <inheritdoc/>
    /// <remarks>An account that is not in the cache throws <see cref="MsalUiRequiredException"/>, the same exception MSAL uses when a refresh token is no longer accepted.</remarks>
    public async Task<SignInOutcome> AcquireAsync(byte[] cache, string accountIdentifier, CancellationToken cancellationToken)
    {
        var application = MsalApplication.Create(options, cache);
        var account = await application.Client.GetAccountAsync(accountIdentifier).ConfigureAwait(false) ?? throw new MsalUiRequiredException(MsalError.UserNullError, AccountMissingMessage).WithHelpLink(OneDriveErrorCodes.AccountNotSignedIn);
        var result = await application.Client.AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken).ConfigureAwait(false);

        return new SignInOutcome(new ProviderAccount(result.Account.HomeAccountId.Identifier, result.Account.Username), application.Cache, result.AccessToken, result.ExpiresOn);
    }
}
