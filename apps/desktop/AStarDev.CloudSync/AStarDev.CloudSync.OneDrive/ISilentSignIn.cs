namespace AStarDev.CloudSync.OneDrive;

/// <summary>Seam over MSAL's silent token acquisition. May throw; the caller captures and maps the exception.</summary>
internal interface ISilentSignIn
{
    /// <summary>Gets a fresh access token for one account from <paramref name="cache"/>, without any user interaction.</summary>
    /// <param name="cache">The token cache as it was last saved.</param>
    /// <param name="accountIdentifier">The provider's id for the account to sign in.</param>
    /// <param name="cancellationToken">Cancelling abandons the request.</param>
    /// <returns>The account, the token cache as MSAL left it, and the new access token. Throws when the account is not in the cache or the provider needs the user to sign in again.</returns>
    Task<SignInOutcome> AcquireAsync(byte[] cache, string accountIdentifier, CancellationToken cancellationToken);
}
