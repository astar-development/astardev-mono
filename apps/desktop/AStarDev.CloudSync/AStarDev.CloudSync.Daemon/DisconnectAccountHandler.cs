using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="DisconnectAccount"/> by forgetting the stored sign-in and the account, then announcing <see cref="AccountDisconnected"/>.</summary>
internal sealed class DisconnectAccountHandler(ICloudProvider provider, IAccountStore store, AuditJournal audit, IIpcServer server, ILogger<DisconnectAccountHandler> logger)
{
    /// <summary>Removes the sign-in first and the account second, so a failure leaves an account that can be disconnected again.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once both are gone and <see cref="AccountDisconnected"/> is published, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the account is unknown,
    /// the keyring cannot be cleared or the store fails. An <see cref="IpcError"/> only when the event could not be published. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(DisconnectAccount command, CancellationToken cancellationToken)
        => await store.GetAsync(new AccountId(command.AccountId), cancellationToken) switch
        {
            Ok<Account> account => await ForgetTokenAsync(account.Value, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "removed"))
        };

    private async Task<Result<CommandResult, IpcError>> ForgetTokenAsync(Account account, CancellationToken cancellationToken)
        => await provider.DisconnectAsync(account.AccountIdentifier, cancellationToken) switch
        {
            Ok<Unit> => await RemoveAsync(account, cancellationToken),
            Fail<ProviderError> fail => Reject(ProviderErrorMessages.Describe(fail.Error))
        };

    private async Task<Result<CommandResult, IpcError>> RemoveAsync(Account account, CancellationToken cancellationToken)
        => await store.RemoveAsync(account.Id, cancellationToken) switch
        {
            Ok<Unit> => await AnnounceAsync(account, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "removed"))
        };

    private async Task<Result<CommandResult, IpcError>> AnnounceAsync(Account account, CancellationToken cancellationToken)
    {
        DaemonLog.AccountDisconnected(logger, account.Id.Value);
        await audit.RecordAsync("AccountDisconnected", account.Id.Value, new { accountId = account.Id.Value }, cancellationToken);

        return await server.PublishAsync(new AccountDisconnected(account.Id.Value), cancellationToken) switch
        {
            Ok<Unit> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
            Fail<IpcError> fail => fail
        };
    }

    private Ok<CommandResult> Reject(string message)
    {
        DaemonLog.DisconnectAccountFailed(logger, message);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
