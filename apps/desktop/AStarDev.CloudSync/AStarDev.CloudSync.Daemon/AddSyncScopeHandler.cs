using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="AddSyncScope"/> by storing the folder as a scope of the job in the <see cref="ISyncScopeStore"/>.</summary>
internal sealed class AddSyncScopeHandler(ISyncScopeStore store, AuditJournal audit, ILogger<AddSyncScopeHandler> logger)
{
    /// <summary>Stores the folder as a scope of the job.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once stored and recorded in the audit trail, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the job is unknown, already holds the folder or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(AddSyncScope command, CancellationToken cancellationToken)
        => await store.AddAsync(new SyncScope(new SyncJobId(command.SyncJobId), new RemoteItemId(command.RemoteRootId)), cancellationToken) switch
        {
            Ok<Unit> => await RecordAsync(command, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "saved", "folder selection"))
        };

    private async Task<Result<CommandResult, IpcError>> RecordAsync(AddSyncScope command, CancellationToken cancellationToken)
    {
        await audit.RecordAsync("SyncScopeAdded", command.SyncJobId, new { syncJobId = command.SyncJobId, remoteRootId = command.RemoteRootId }, cancellationToken);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));
    }

    private Ok<CommandResult> Reject(string message)
    {
        DaemonLog.AddSyncScopeFailed(logger, message);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
