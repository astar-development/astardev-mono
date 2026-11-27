using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Renews an account's access token from the stored sign-in shortly before it expires, so calls never go out with a stale token.</summary>
internal sealed class TokenRefresher(OneDriveAccounts accounts, TimeProvider time)
{
    private static readonly TimeSpan RenewMargin = TimeSpan.FromMinutes(5);

    /// <summary>Runs <paramref name="renew"/> when the account's token expires within five minutes; otherwise does nothing.</summary>
    /// <param name="accountIdentifier">The provider's id for the account.</param>
    /// <param name="renew">Signs the account in again silently and stores the new token.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait for another renewal and the renewal itself.</param>
    /// <returns>Success when the token is fresh or was renewed, or the <see cref="ProviderError"/> that <paramref name="renew"/> returned, such as <see cref="AuthenticationFailed"/> when the user must sign in again. Concurrent calls for one account renew once.</returns>
    public async Task<Result<Unit, ProviderError>> EnsureFreshAsync(string accountIdentifier, Func<string, CancellationToken, Task<Result<ProviderAccount, ProviderError>>> renew, CancellationToken cancellationToken)
    {
        var holder = accounts.For(accountIdentifier).Holder;
        if (!IsDue(holder)) return new Ok<Unit>(default);

        await holder.RenewalGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return IsDue(holder) ? Renewed(await renew(accountIdentifier, cancellationToken).ConfigureAwait(false)) : new Ok<Unit>(default);
        }
        finally
        {
            holder.RenewalGate.Release();
        }
    }

    private bool IsDue(AccessTokenHolder holder) => holder.IsDue(time.GetUtcNow(), RenewMargin);

    private static Result<Unit, ProviderError> Renewed(Result<ProviderAccount, ProviderError> result)
        => result switch
        {
            Ok<ProviderAccount> => new Ok<Unit>(default),
            Fail<ProviderError> fail => fail
        };
}
