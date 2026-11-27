namespace AStarDev.CloudSync.OneDrive;

/// <summary>Seam over MSAL's account removal. May throw; the caller captures and maps the exception.</summary>
internal interface ISignOut
{
    /// <summary>Removes one account from <paramref name="cache"/>, leaving every other account in it.</summary>
    /// <param name="cache">The token cache as it was last saved.</param>
    /// <param name="accountIdentifier">The provider's id for the account to remove. An account not in the cache changes nothing.</param>
    /// <param name="cancellationToken">Cancelling abandons the removal.</param>
    /// <returns>The token cache without the account, and whether any account remains in it.</returns>
    Task<RemainingCache> RemoveAsync(byte[] cache, string accountIdentifier, CancellationToken cancellationToken);
}
