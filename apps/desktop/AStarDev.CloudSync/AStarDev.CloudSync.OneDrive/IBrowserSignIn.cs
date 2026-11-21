namespace AStarDev.CloudSync.OneDrive;

/// <summary>Seam over MSAL's interactive sign-in. May throw; the caller captures and maps the exception.</summary>
internal interface IBrowserSignIn
{
    Task<SignInOutcome> SignInAsync(byte[] cache, CancellationToken cancellationToken);
}
