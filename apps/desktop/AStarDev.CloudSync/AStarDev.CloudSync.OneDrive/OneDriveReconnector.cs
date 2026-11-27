using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Signs a stored OneDrive account in again from the token cache, with no browser.</summary>
internal sealed class OneDriveReconnector(ITokenCacheStore store, OneDriveAccounts accounts, ISilentSignIn signIn)
{
    /// <summary>Loads the stored cache, acquires a token silently and saves the cache MSAL refreshed.</summary>
    /// <returns>The account, or a <see cref="ProviderError"/>. Nothing is saved and no token is kept after a failure.</returns>
    public async Task<Result<ProviderAccount, ProviderError>> ReconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
        => await store.LoadAsync(cancellationToken).ConfigureAwait(false) switch
        {
            Ok<byte[]> ok => await AcquireAsync(ok.Value, accountIdentifier, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    private async Task<Result<ProviderAccount, ProviderError>> AcquireAsync(byte[] cache, string accountIdentifier, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(token => signIn.AcquireAsync(cache, accountIdentifier, token), cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(AuthenticationErrors.From) switch
        {
            Ok<SignInOutcome> ok => await SaveAsync(ok.Value, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };
    }

    private async Task<Result<ProviderAccount, ProviderError>> SaveAsync(SignInOutcome outcome, CancellationToken cancellationToken)
        => await store.SaveAsync(outcome.Cache, cancellationToken).ConfigureAwait(false) switch
        {
            Ok<Unit> => Authorised(outcome),
            Fail<ProviderError> fail => fail
        };

    private Ok<ProviderAccount> Authorised(SignInOutcome outcome)
    {
        accounts.Set(outcome.Account.AccountIdentifier, outcome.AccessToken);

        return new Ok<ProviderAccount>(outcome.Account);
    }
}
