using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Renews an account's access token from the stored sign-in shortly before it expires, so calls never go out with a stale token.</summary>
internal sealed class TokenRefresher(OneDriveAccounts accounts, TimeProvider time)
{
    private static readonly TimeSpan RenewMargin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim renewalGate = new(1, 1);

    /// <summary>Runs <paramref name="renew"/> when the account's token expires within five minutes; otherwise does nothing.</summary>
    /// <param name="accountIdentifier">The provider's id for the account.</param>
    /// <param name="renew">Signs the account in again silently and stores the new token.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait for another renewal and the renewal itself.</param>
    /// <returns>Success when the token is fresh or was renewed, or the <see cref="ProviderError"/> that <paramref name="renew"/> returned, such as <see cref="AuthenticationFailed"/> when the user must sign in again. A failed renewal is hidden while the held token is still valid, so a brief outage in the last five minutes does not fail calls the old token can serve. Renewals run one at a time across accounts, because they share one token cache; concurrent calls for one account renew once.</returns>
    public async Task<Result<Unit, ProviderError>> EnsureFreshAsync(string accountIdentifier, Func<string, CancellationToken, Task<Result<ProviderAccount, ProviderError>>> renew, CancellationToken cancellationToken)
    {
        var holder = accounts.For(accountIdentifier).Holder;
        if (!IsDue(holder)) return new Ok<Unit>(default);

        await renewalGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return IsDue(holder) ? Renewed(await renew(accountIdentifier, cancellationToken).ConfigureAwait(false), holder) : new Ok<Unit>(default);
        }
        finally
        {
            renewalGate.Release();
        }
    }

    private bool IsDue(AccessTokenHolder holder) => holder.IsDue(time.GetUtcNow(), RenewMargin);

    private Result<Unit, ProviderError> Renewed(Result<ProviderAccount, ProviderError> result, AccessTokenHolder holder)
        => result switch
        {
            Ok<ProviderAccount> => new Ok<Unit>(default),
            Fail<ProviderError> when !holder.IsDue(time.GetUtcNow(), TimeSpan.Zero) => new Ok<Unit>(default),
            Fail<ProviderError> fail => fail
        };
}
