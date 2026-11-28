using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="DisconnectAccount"/> by forgetting the stored sign-in, stopping the account's sync and forgetting the account, then announcing <see cref="AccountDisconnected"/>.</summary>
internal sealed class DisconnectAccountHandler(ICloudProvider provider, IAccountStore store, ISyncJobStore jobs, SyncRunner runner, LocalObservationRunner observation, OperationScheduler scheduler, AuditJournal audit, IIpcServer server, ILogger<DisconnectAccountHandler> logger)
{
    /// <summary>
    /// Removes the sign-in first and the account second, so a failure leaves an account that can be disconnected again. Between the two, the crawl, delta processing, local observation and operations of the account's job
    /// are stopped and awaited, so no work writes to the removed job; a job stopped this way stays stopped when the account cannot be removed.
    /// </summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once both are gone and <see cref="AccountDisconnected"/> is published, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the account is unknown,
    /// the keyring cannot be cleared or the store fails. An <see cref="IpcError"/> only when the event could not be published. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(DisconnectAccount command, CancellationToken cancellationToken)
        => await store.GetAsync(new AccountId(command.AccountId), cancellationToken) switch
        {
            Ok<Account> account => await ForgetTokenAsync(account.Value, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "removed"), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> ForgetTokenAsync(Account account, CancellationToken cancellationToken)
        => await provider.DisconnectAsync(account.AccountIdentifier, cancellationToken) switch
        {
            Ok<Unit> => await StopSyncAsync(account, cancellationToken),
            Fail<ProviderError> fail => Reject(ProviderErrorMessages.Describe(fail.Error), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> StopSyncAsync(Account account, CancellationToken cancellationToken)
        => await jobs.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<SyncJob>> ok => await RemoveStoppedAsync(account, [.. ok.Value.Where(job => job.AccountId == account.Id).Select(job => job.Id)], cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "removed"), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> RemoveStoppedAsync(Account account, IReadOnlyList<SyncJobId> accountJobs, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var job in accountJobs) await StopAsync(job, cancellationToken);

            return await RemoveAsync(account, cancellationToken);
        }
        finally
        {
            foreach (var job in accountJobs) scheduler.Release(job);
        }
    }

    private async Task StopAsync(SyncJobId job, CancellationToken cancellationToken)
    {
        await runner.StopAsync(job, cancellationToken);
        await observation.StopAsync(job, cancellationToken);
        await scheduler.HoldAsync(job, cancellationToken);
    }

    private async Task<Result<CommandResult, IpcError>> RemoveAsync(Account account, CancellationToken cancellationToken)
        => await store.RemoveAsync(account.Id, cancellationToken) switch
        {
            Ok<Unit> => await AnnounceAsync(account, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "removed"), ErrorInfo.CodeOf(fail.Error.Value))
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

    private Ok<CommandResult> Reject(string message, ErrorCode errorCode)
    {
        DaemonLog.DisconnectAccountFailed(logger, message, errorCode);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
