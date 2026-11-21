using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Forgets the stored OneDrive sign-in: the Secret Service entry and the access token held in memory.</summary>
internal sealed class OneDriveDisconnector(ITokenCacheStore store, AccessTokenHolder holder)
{
    /// <summary>Removes the stored token cache and drops the held token.</summary>
    /// <returns>Success when nothing remains stored, also when nothing was; or the keyring failure, in which case the held token is kept.</returns>
    public async Task<Result<Unit, ProviderError>> DisconnectAsync(CancellationToken cancellationToken)
        => await store.ClearAsync(cancellationToken).ConfigureAwait(false) switch
        {
            Ok<Unit> ok => Forget(ok),
            Fail<ProviderError> fail => fail
        };

    private Ok<Unit> Forget(Ok<Unit> cleared)
    {
        holder.Clear();

        return cleared;
    }
}
