using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetDeadLetters"/> from the <see cref="IOperationStore"/>.</summary>
internal sealed class DeadLettersQuery(IOperationStore store)
{
    /// <summary>Lists the operations that gave up after their retries.</summary>
    /// <returns>The dead-lettered operations, empty when there are none, or <see cref="HandlerFailed"/> with a message for the user when the store fails. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<DeadLettersView, IpcError>> HandleAsync(GetDeadLetters query, CancellationToken cancellationToken)
        => await store.ListDeadLettersAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<Operation>> ok => new Ok<DeadLettersView>(new DeadLettersView(ok.Value.Select(operation => new DeadLetterView(operation.Id.Value, operation.ItemId.Value, operation.Type.ToString(), operation.Attempts, operation.LastError, operation.CreatedUtc)).ToList())),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "dead-lettered operations")))
        };
}
