using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where connected accounts are kept between runs. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IAccountStore
{
    /// <summary>Stores a newly connected account.</summary>
    /// <param name="account">The account to keep.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves nothing stored.</param>
    /// <returns>Success, <see cref="AccountAlreadyExists"/> when the identity or provider account is taken, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> AddAsync(Account account, CancellationToken cancellationToken);

    /// <summary>Finds one account by our identity.</summary>
    /// <param name="id">The identity to look up.</param>
    /// <param name="cancellationToken">Cancelling abandons the lookup.</param>
    /// <returns>The account, <see cref="AccountNotFound"/> when there is none, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Account, StoreError>> GetAsync(AccountId id, CancellationToken cancellationToken);

    /// <summary>Lists every stored account.</summary>
    /// <param name="cancellationToken">Cancelling abandons the listing.</param>
    /// <returns>The accounts, oldest first; empty when none are connected. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<Account>, StoreError>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Records whether the account's stored sign-in still works.</summary>
    /// <param name="id">The account to change.</param>
    /// <param name="status">The new status. Setting the status the account already has succeeds and changes nothing.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the status as it was.</param>
    /// <returns>Success, <see cref="AccountNotFound"/> when there is no such account, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> UpdateStatusAsync(AccountId id, AccountStatus status, CancellationToken cancellationToken);

    /// <summary>Deletes an account.</summary>
    /// <param name="id">The account to delete.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the account stored.</param>
    /// <returns>Success, <see cref="AccountNotFound"/> when there is no such account, or <see cref="StorageFailed"/>, for example while sync jobs still use the account.</returns>
    Task<Result<Unit, StoreError>> RemoveAsync(AccountId id, CancellationToken cancellationToken);
}
