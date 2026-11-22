using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="IgnoreOperation"/> by dropping the dead-lettered operation from the queue.</summary>
internal sealed class IgnoreOperationHandler(IOperationStore store)
{
    /// <summary>Deletes the operation so it is never run.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once the operation is gone, or <see cref="CommandOutcome.Rejected"/> with a message for the user when it is not dead-lettered or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(IgnoreOperation command, CancellationToken cancellationToken)
        => await store.DiscardDeadLetterAsync(new OperationId(command.OperationId), cancellationToken) switch
        {
            Ok<Unit> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
            Fail<StoreError> fail => new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, StoreErrorMessages.Describe(fail.Error, "dropped", "operation")))
        };
}
