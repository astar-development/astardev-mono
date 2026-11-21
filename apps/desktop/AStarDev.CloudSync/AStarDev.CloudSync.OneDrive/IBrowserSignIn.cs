namespace AStarDev.CloudSync.OneDrive;

/// <summary>Seam over MSAL's interactive sign-in. May throw; the caller captures and maps the exception.</summary>
internal interface IBrowserSignIn
{
    /// <summary>Signs the user in through the browser, starting from <paramref name="cache"/>, which is empty on first run.</summary>
    /// <returns>The signed-in account and the token cache as MSAL left it, to be saved for the next run.</returns>
    Task<SignInOutcome> SignInAsync(byte[] cache, CancellationToken cancellationToken);
}
