using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Signs in to OneDrive, keeps the token cache in the <see cref="ITokenCacheStore"/> and hands the access token to the account's holder in <see cref="OneDriveAccounts"/>.</summary>
internal sealed class OneDriveConnector(ITokenCacheStore store, OneDriveAccounts accounts, IBrowserSignIn signIn)
{
    /// <summary>Loads the stored token cache, runs the browser sign-in with it, and saves the updated cache before returning the account.</summary>
    /// <returns>The signed-in account, or a <see cref="ProviderError"/> when the keyring cannot be read or written or sign-in fails. Nothing is saved and no token is kept after a failed sign-in.</returns>
    public async Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken)
    {
        var loaded = await store.LoadAsync(cancellationToken).ConfigureAwait(false);

        return loaded switch
        {
            Ok<byte[]> ok => await SignInAndSaveAsync(ok.Value, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };
    }

    private async Task<Result<ProviderAccount, ProviderError>> SignInAndSaveAsync(byte[] cache, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(token => signIn.SignInAsync(cache, token), cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(AuthenticationErrors.From) switch
        {
            Ok<SignInOutcome> ok => await SaveAsync(ok.Value, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };
    }

    private async Task<Result<ProviderAccount, ProviderError>> SaveAsync(SignInOutcome outcome, CancellationToken cancellationToken)
    {
        var saved = await store.SaveAsync(outcome.Cache, cancellationToken).ConfigureAwait(false);

        return saved switch
        {
            Ok<Unit> => Authorised(outcome),
            Fail<ProviderError> fail => fail
        };
    }

    private Ok<ProviderAccount> Authorised(SignInOutcome outcome)
    {
        accounts.Set(outcome.Account.AccountIdentifier, outcome.AccessToken);

        return new Ok<ProviderAccount>(outcome.Account);
    }
}
