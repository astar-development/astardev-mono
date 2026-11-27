using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Signs one OneDrive account out and leaves the other accounts signed in.</summary>
internal sealed class OneDriveDisconnector(ITokenCacheStore store, OneDriveAccounts accounts, ISignOut signOut)
{
    /// <summary>Removes the account from the stored cache, clears the whole cache when no account remains, and forgets the account's access token.</summary>
    /// <returns>Success, also when the account was not stored, or a <see cref="ProviderError"/>. The token is kept after a failure.</returns>
    public async Task<Result<Unit, ProviderError>> DisconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
        => await store.LoadAsync(cancellationToken).ConfigureAwait(false) switch
        {
            Ok<byte[]> ok => await RemoveAsync(ok.Value, accountIdentifier, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    private async Task<Result<Unit, ProviderError>> RemoveAsync(byte[] cache, string accountIdentifier, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(token => signOut.RemoveAsync(cache, accountIdentifier, token), cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(AuthenticationErrors.From) switch
        {
            Ok<RemainingCache> ok => await KeepAsync(ok.Value, accountIdentifier, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };
    }

    private async Task<Result<Unit, ProviderError>> KeepAsync(RemainingCache remaining, string accountIdentifier, CancellationToken cancellationToken)
        => await (remaining.HasAccounts ? store.SaveAsync(remaining.Cache, cancellationToken) : store.ClearAsync(cancellationToken)).ConfigureAwait(false) switch
        {
            Ok<Unit> ok => Forget(ok, accountIdentifier),
            Fail<ProviderError> fail => fail
        };

    private Ok<Unit> Forget(Ok<Unit> saved, string accountIdentifier)
    {
        accounts.Clear(accountIdentifier);

        return saved;
    }
}
