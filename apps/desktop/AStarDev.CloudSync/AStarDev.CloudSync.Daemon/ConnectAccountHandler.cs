using System.Diagnostics;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="ConnectAccount"/> by signing in through <see cref="ICloudProvider"/>, keeping the account in the <see cref="IAccountStore"/> and announcing the result with <see cref="AccountConnected"/>.</summary>
internal sealed class ConnectAccountHandler(ICloudProvider provider, IAccountStore store, AuditJournal audit, IIpcServer server, TimeProvider timeProvider, ILogger<ConnectAccountHandler> logger)
{
    /// <summary>
    /// Signs in and, on success, stores the account (or marks the already stored one <see cref="AccountStatus.Connected"/>, which is how a user signs in again) and publishes <see cref="AccountConnected"/> before answering.
    /// </summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once connected and stored, or <see cref="CommandOutcome.Rejected"/> with the provider's or the store's failure worded for the user and the id of the daemon trace that handled it. An <see cref="IpcError"/>
    /// only when the event could not be published. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(ConnectAccount command, CancellationToken cancellationToken)
        => await provider.ConnectAsync(cancellationToken) switch
        {
            Ok<ProviderAccount> account => await KeepAsync(account.Value, cancellationToken),
            Fail<ProviderError> fail => Reject(ProviderErrorMessages.Describe(fail.Error))
        };

    private async Task<Result<CommandResult, IpcError>> KeepAsync(ProviderAccount account, CancellationToken cancellationToken)
        => await StoreAsync(account, cancellationToken) switch
        {
            Ok<AccountId> stored => await AnnounceAsync(account, stored.Value, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "saved"))
        };

    private async Task<Result<AccountId, StoreError>> StoreAsync(ProviderAccount account, CancellationToken cancellationToken)
        => await store.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<Account>> list => await AddOrRestoreAsync(account, list.Value, cancellationToken),
            Fail<StoreError> fail => fail
        };

    private async Task<Result<AccountId, StoreError>> AddOrRestoreAsync(ProviderAccount account, IReadOnlyList<Account> stored, CancellationToken cancellationToken)
        => stored.FirstOrDefault(known => known.ProviderType == ApplicationConstants.OneDriveProviderType && known.AccountIdentifier == account.AccountIdentifier) switch
        {
            { } known => Keep(known.Id, await store.UpdateStatusAsync(known.Id, AccountStatus.Connected, cancellationToken)),
            null => await AddAsync(account, cancellationToken)
        };

    private async Task<Result<AccountId, StoreError>> AddAsync(ProviderAccount account, CancellationToken cancellationToken)
    {
        var added = new Account(new AccountId(Guid.NewGuid()), ApplicationConstants.OneDriveProviderType, account.DisplayName, account.AccountIdentifier, timeProvider.GetUtcNow());

        return Keep(added.Id, await store.AddAsync(added, cancellationToken));
    }

    private static Result<AccountId, StoreError> Keep(AccountId id, Result<Unit, StoreError> stored)
        => stored switch
        {
            Ok<Unit> => new Ok<AccountId>(id),
            Fail<StoreError> fail => fail
        };

    private async Task<Result<CommandResult, IpcError>> AnnounceAsync(ProviderAccount account, AccountId id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Connected account {AccountId}", id.Value);
        await audit.RecordAsync("AccountConnected", id.Value, new { accountId = id.Value }, cancellationToken);

        return await server.PublishAsync(new AccountConnected(account.DisplayName), cancellationToken) switch
        {
            Ok<Unit> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
            Fail<IpcError> fail => fail
        };
    }

    private Ok<CommandResult> Reject(string message)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        logger.LogWarning("Connecting an account failed: {Message}", message);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message, traceId));
    }
}
