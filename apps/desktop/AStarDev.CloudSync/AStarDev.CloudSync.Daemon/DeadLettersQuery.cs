using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetDeadLetters"/> from the <see cref="IOperationStore"/>.</summary>
internal sealed class DeadLettersQuery(IOperationStore store)
{
    /// <summary>Reads the requested page of the operations that gave up after their retries, so one answer fits an IPC frame.</summary>
    /// <returns>The page with how many operations gave up in all, empty when there are none; a negative <see cref="GetDeadLetters.Skip"/> counts as zero and a <see cref="GetDeadLetters.Take"/> outside 1 to <see cref="ApplicationConstants.ListLimit"/> is brought into that range. <see cref="HandlerFailed"/> with a message for the user when the store fails. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<DeadLettersView, IpcError>> HandleAsync(GetDeadLetters query, CancellationToken cancellationToken)
        => await store.ListDeadLettersAsync(Paging.Skip(query.Skip), Paging.Take(query.Take), cancellationToken) switch
        {
            Ok<Page<Operation>> ok => new Ok<DeadLettersView>(new DeadLettersView(ok.Value.Total, [.. ok.Value.Items.Select(operation => new DeadLetterView(operation.Id.Value, operation.ItemId.Value, operation.Type.ToString(), operation.Attempts, operation.LastError, operation.CreatedUtc))])),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "dead-lettered operations")))
        };
}
