using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="RemoveSyncScope"/> by deleting the scope from the <see cref="ISyncScopeStore"/>.</summary>
internal sealed class RemoveSyncScopeHandler(ISyncScopeStore store, AuditJournal audit, ILogger<RemoveSyncScopeHandler> logger)
{
    /// <summary>Deletes the folder from the job's scopes.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once removed and recorded in the audit trail, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the job does not hold the folder or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(RemoveSyncScope command, CancellationToken cancellationToken)
        => await store.RemoveAsync(new SyncScope(new SyncJobId(command.SyncJobId), new RemoteItemId(command.RemoteRootId)), cancellationToken) switch
        {
            Ok<Unit> => await RecordAsync(command, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "removed", "folder selection"))
        };

    private async Task<Result<CommandResult, IpcError>> RecordAsync(RemoveSyncScope command, CancellationToken cancellationToken)
    {
        await audit.RecordAsync("SyncScopeRemoved", command.SyncJobId, new { syncJobId = command.SyncJobId, remoteRootId = command.RemoteRootId }, cancellationToken);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));
    }

    private Ok<CommandResult> Reject(string message)
    {
        logger.LogWarning("Removing a sync scope failed: {Message}", message);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
