using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="SetAccountConcurrency"/> by storing the account's new limit in the <see cref="IAccountStore"/>.</summary>
internal sealed class SetAccountConcurrencyHandler(IAccountStore store, AuditJournal audit, ILogger<SetAccountConcurrencyHandler> logger)
{
    /// <summary>Stores the limit when it is one of <see cref="SetAccountConcurrency.AllowedValues"/>.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once stored and recorded in the audit trail, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the value is not allowed, the account is unknown or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(SetAccountConcurrency command, CancellationToken cancellationToken)
        => SetAccountConcurrency.AllowedValues.Contains(command.MaxConcurrentOperations)
            ? await StoreAsync(command, cancellationToken)
            : Reject($"{command.MaxConcurrentOperations} concurrent operations is not allowed; choose 1, 2, 4, 6 or 8.", default);

    private async Task<Result<CommandResult, IpcError>> StoreAsync(SetAccountConcurrency command, CancellationToken cancellationToken)
        => await store.UpdateConcurrencyAsync(new AccountId(command.AccountId), command.MaxConcurrentOperations, cancellationToken) switch
        {
            Ok<Unit> => await RecordAsync(command, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "changed"), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> RecordAsync(SetAccountConcurrency command, CancellationToken cancellationToken)
    {
        await audit.RecordAsync("AccountConcurrencyChanged", command.AccountId, new { accountId = command.AccountId, maxConcurrentOperations = command.MaxConcurrentOperations }, cancellationToken);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));
    }

    private Ok<CommandResult> Reject(string message, ErrorCode errorCode)
    {
        DaemonLog.SetAccountConcurrencyFailed(logger, message, errorCode);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
