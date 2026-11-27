using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetAccounts"/> from the <see cref="IAccountStore"/>.</summary>
internal sealed class AccountsQuery(IAccountStore store)
{
    /// <summary>Lists every stored account with its state.</summary>
    /// <returns>The accounts, oldest first, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<AccountsView, IpcError>> HandleAsync(GetAccounts query, CancellationToken cancellationToken)
        => await store.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<Account>> accounts => new Ok<AccountsView>(new AccountsView([.. accounts.Value.Select(ToView)])),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read")))
        };

    private static AccountView ToView(Account account) => new(account.Id.Value, account.DisplayName, account.ProviderType, ToState(account.Status), account.MaxConcurrentOperations);

    private static AccountState ToState(AccountStatus status)
        => status == AccountStatus.ReauthenticationRequired ? AccountState.ReauthenticationRequired : AccountState.Connected;
}
