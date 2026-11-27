using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>MSAL public client removing one account from the saved cache.</summary>
internal sealed class MsalSignOut(OneDriveOptions options) : ISignOut
{
    /// <inheritdoc/>
    public async Task<RemainingCache> RemoveAsync(byte[] cache, string accountIdentifier, CancellationToken cancellationToken)
    {
        var application = MsalApplication.Create(options, cache);
        if (await application.Client.GetAccountAsync(accountIdentifier).ConfigureAwait(false) is { } account) await application.Client.RemoveAsync(account).ConfigureAwait(false);
        var remaining = await application.Client.GetAccountsAsync().ConfigureAwait(false);

        return new RemainingCache(application.Cache, remaining.Any());
    }
}
