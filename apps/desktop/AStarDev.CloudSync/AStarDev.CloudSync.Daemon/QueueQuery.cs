using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetQueue"/> from the <see cref="IOperationStore"/>, the job and account stores and the item state, and publishes <see cref="QueueChanged"/> when the answer changes.</summary>
internal sealed class QueueQuery(IOperationStore operations, OperationLookup lookup, IIpcServer server)
{
    private readonly object gate = new();
    private Option<QueueView> published = Option.None<QueueView>();

    /// <summary>Counts the operations in each status and lists the pending and running ones, newest first.</summary>
    /// <returns>
    /// The queue, with dead-lettered operations counted as failed and an empty list when nothing waits or runs. An operation whose item or account the daemon no longer knows keeps an empty path or provider.
    /// <see cref="HandlerFailed"/> carries a store failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<QueueView, IpcError>> HandleAsync(GetQueue query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await operations.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<Operation>> ok => await DescribeAsync(ok.Value, cancellationToken),
            Fail<StoreError> fail => Failure(fail.Error)
        };
    }

    /// <summary>Publishes <see cref="QueueChanged"/> when the queue differs from the one published last. The first call always publishes.</summary>
    /// <returns>Success when nothing needed publishing or the event went out; otherwise the read or publish failure. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<Unit, IpcError>> PublishIfChangedAsync(CancellationToken cancellationToken)
        => await HandleAsync(new GetQueue(), cancellationToken) switch
        {
            Ok<QueueView> queue => HasChanged(queue.Value) ? await server.PublishAsync(new QueueChanged(queue.Value), cancellationToken) : new Ok<Unit>(default),
            Fail<IpcError> fail => fail
        };

    private async Task<Result<QueueView, IpcError>> DescribeAsync(IReadOnlyList<Operation> all, CancellationToken cancellationToken)
    {
        var open = all.Where(operation => operation.Status is OperationStatus.Pending or OperationStatus.Running).OrderByDescending(operation => operation.CreatedUtc).ToList();
        var providers = await lookup.ProvidersAsync(cancellationToken);
        if (providers.Value is Fail<StoreError> providersFailure) return Failure(providersFailure.Error);

        var items = await lookup.ItemsAsync(open.Select(operation => operation.SyncJobId).Distinct().ToList(), cancellationToken);
        if (items.Value is Fail<StoreError> itemsFailure) return Failure(itemsFailure.Error);

        var providerOf = ((Ok<IReadOnlyDictionary<SyncJobId, string>>)providers.Value).Value;
        var stateOf = ((Ok<IReadOnlyDictionary<ItemId, CanonicalItemState>>)items.Value).Value;

        return new Ok<QueueView>(new QueueView(
            Count(all, OperationStatus.Pending),
            Count(all, OperationStatus.Running),
            Count(all, OperationStatus.Completed),
            Count(all, OperationStatus.Failed) + Count(all, OperationStatus.DeadLetter),
            open.Select(operation => ViewOf(operation, providerOf.GetValueOrDefault(operation.SyncJobId, string.Empty), stateOf.GetValueOrDefault(operation.ItemId))).ToList()));
    }

    private bool HasChanged(QueueView queue)
    {
        lock (gate)
        {
            if (published.Value is Some<QueueView> last && Same(last.Value, queue)) return false;

            published = Option.Some(queue);

            return true;
        }
    }

    private static bool Same(QueueView left, QueueView right)
        => left with { Operations = [] } == right with { Operations = [] } && left.Operations.SequenceEqual(right.Operations);

    private static int Count(IReadOnlyList<Operation> all, OperationStatus status) => all.Count(operation => operation.Status == status);

    private static QueuedOperationView ViewOf(Operation operation, string provider, CanonicalItemState? state)
        => new(operation.Id.Value, provider, operation.Type.ToString(), operation.Status.ToString(), OperationLookup.PathOf(state), OperationLookup.SizeOf(state), operation.CreatedUtc);

    private static Fail<IpcError> Failure(StoreError error) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", "operation queue")));
}
