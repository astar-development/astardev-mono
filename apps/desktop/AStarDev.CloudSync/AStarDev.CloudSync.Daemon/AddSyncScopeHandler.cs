using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="AddSyncScope"/> by storing the folder as a scope of the job in the <see cref="ISyncScopeStore"/>.</summary>
internal sealed class AddSyncScopeHandler(ISyncScopeStore store, ILogger<AddSyncScopeHandler> logger)
{
    /// <summary>Stores the folder as a scope of the job.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once stored, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the job is unknown, already holds the folder or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(AddSyncScope command, CancellationToken cancellationToken)
        => await store.AddAsync(new SyncScope(new SyncJobId(command.SyncJobId), new RemoteItemId(command.RemoteRootId)), cancellationToken) switch
        {
            Ok<Unit> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "saved", "folder selection"))
        };

    private Ok<CommandResult> Reject(string message)
    {
        logger.LogWarning("Adding a sync scope failed: {Message}", message);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}
