using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Signs in to OneDrive and keeps the token cache in the <see cref="ITokenCacheStore"/>.</summary>
internal sealed class OneDriveConnector(ITokenCacheStore store, IBrowserSignIn signIn)
{
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
            Ok<Unit> => new Ok<ProviderAccount>(outcome.Account),
            Fail<ProviderError> fail => fail
        };
    }
}
