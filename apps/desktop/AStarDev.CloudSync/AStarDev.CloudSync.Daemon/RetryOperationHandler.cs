using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="RetryOperation"/> by putting the dead-lettered operation back in the queue.</summary>
internal sealed class RetryOperationHandler(IOperationStore store)
{
    /// <summary>Requeues the operation with its attempts reset.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once the operation is pending again, or <see cref="CommandOutcome.Rejected"/> with a message for the user when it is not dead-lettered or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(RetryOperation command, CancellationToken cancellationToken)
        => await store.RequeueDeadLetterAsync(new OperationId(command.OperationId), cancellationToken) switch
        {
            Ok<Unit> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
            Fail<StoreError> fail => new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, StoreErrorMessages.Describe(fail.Error, "requeued", "operation")))
        };
}
