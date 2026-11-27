using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Records that a stored account must sign in again, so the user is asked to reconnect it instead of its calls failing for ever.</summary>
internal sealed class ReauthenticationMarker(IAccountStore store, ILogger<ReauthenticationMarker> logger)
{
    /// <summary>Sets the stored account to <see cref="AccountStatus.ReauthenticationRequired"/>. An unknown account or one already marked is left alone, and a store failure is logged, never thrown, so the failing call still reports its own error.</summary>
    /// <param name="accountIdentifier">The provider's id for the account, as <see cref="ProviderAccount.AccountIdentifier"/> reported it.</param>
    /// <param name="cancellationToken">Cancelling abandons the update before it is written.</param>
    public async Task MarkAsync(string accountIdentifier, CancellationToken cancellationToken)
    {
        var outcome = await store.ListAsync(cancellationToken).ConfigureAwait(false) switch
        {
            Ok<IReadOnlyList<Account>> list => await UpdateAsync(list.Value.Where(account => account.AccountIdentifier == accountIdentifier).ToList(), cancellationToken).ConfigureAwait(false),
            Fail<StoreError> fail => fail
        };

        if (outcome is Fail<StoreError> failed) OneDriveLog.AccountNotMarked(logger, ErrorInfo.CodeOf(failed.Error.Value));
    }

    private async Task<Result<Unit, StoreError>> UpdateAsync(List<Account> matches, CancellationToken cancellationToken)
        => matches switch
        {
            [{ Status: not AccountStatus.ReauthenticationRequired } account] => await store.UpdateStatusAsync(account.Id, AccountStatus.ReauthenticationRequired, cancellationToken).ConfigureAwait(false),
            _ => new Ok<Unit>(default)
        };
}
