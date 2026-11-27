using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Turns the account id an IPC query carries into the provider's id for that account.</summary>
internal static class AccountIdentifiers
{
    /// <summary>Looks the account up in the store.</summary>
    /// <returns>The <see cref="Account.AccountIdentifier"/>, or <see cref="HandlerFailed"/> worded for the user when the account is unknown or the store fails.</returns>
    public static async Task<Result<string, IpcError>> FindAsync(IAccountStore store, Guid accountId, CancellationToken cancellationToken)
        => await store.GetAsync(new AccountId(accountId), cancellationToken) switch
        {
            Ok<Account> account => new Ok<string>(account.Value.AccountIdentifier),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "account")))
        };
}
