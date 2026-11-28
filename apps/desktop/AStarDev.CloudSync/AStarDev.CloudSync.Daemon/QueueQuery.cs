using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetQueue"/> from the <see cref="IOperationStore"/>, the job and account stores and the item state, and publishes <see cref="QueueChanged"/> when the answer changes.</summary>
internal sealed class QueueQuery(IOperationStore operations, OperationLookup lookup, IIpcServer server, IDatabaseWrites writes)
{
    private static readonly OperationStatus[] Open = [OperationStatus.Pending, OperationStatus.Running];

    private readonly StoreReadCache<QueueView> reads = new(writes);
    private readonly object gate = new();
    private Option<QueueView> published = Option.None<QueueView>();

    /// <summary>Counts the operations in each status and lists the newest pending and running ones, at most <see cref="ApplicationConstants.QueueListLimit"/>, so one answer always fits an IPC frame however long the queue is.</summary>
    /// <returns>
    /// The queue, with dead-lettered operations counted as failed and an empty list when nothing waits or runs. An operation whose item or account the daemon no longer knows keeps an empty path or provider.
    /// <see cref="HandlerFailed"/> carries a store failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<QueueView, IpcError>> HandleAsync(GetQueue query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var counts = await operations.CountByStatusAsync(cancellationToken);
        if (counts.Value is Fail<StoreError> countsFailure) return Failure(countsFailure.Error);

        return await operations.ListNewestAsync(Open, ApplicationConstants.QueueListLimit, cancellationToken) switch
        {
            Ok<IReadOnlyList<Operation>> ok => await DescribeAsync(((Ok<IReadOnlyDictionary<OperationStatus, int>>)counts.Value).Value, ok.Value, cancellationToken),
            Fail<StoreError> fail => Failure(fail.Error)
        };
    }

    /// <summary>Publishes <see cref="QueueChanged"/> when the queue differs from the one published last. The first call always publishes. Reads nothing while no write was committed since the last read, since every value of the queue comes from the database.</summary>
    /// <returns>Success when nothing needed publishing or the event went out; otherwise the read or publish failure. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<Unit, IpcError>> PublishIfChangedAsync(CancellationToken cancellationToken)
        => await reads.ReadAsync(token => HandleAsync(new GetQueue(), token), cancellationToken) switch
        {
            Ok<QueueView> queue => HasChanged(queue.Value) ? await server.PublishAsync(new QueueChanged(queue.Value), cancellationToken) : new Ok<Unit>(default),
            Fail<IpcError> fail => fail
        };

    private async Task<Result<QueueView, IpcError>> DescribeAsync(IReadOnlyDictionary<OperationStatus, int> counts, IReadOnlyList<Operation> open, CancellationToken cancellationToken)
    {
        var providers = await lookup.ProvidersAsync(cancellationToken);
        if (providers.Value is Fail<StoreError> providersFailure) return Failure(providersFailure.Error);

        var items = await lookup.ItemsAsync(open.Select(operation => operation.SyncJobId).Distinct().ToList(), cancellationToken);
        if (items.Value is Fail<StoreError> itemsFailure) return Failure(itemsFailure.Error);

        var providerOf = ((Ok<IReadOnlyDictionary<SyncJobId, string>>)providers.Value).Value;
        var stateOf = ((Ok<IReadOnlyDictionary<ItemId, CanonicalItemState>>)items.Value).Value;

        return new Ok<QueueView>(new QueueView(
            Count(counts, OperationStatus.Pending),
            Count(counts, OperationStatus.Running),
            Count(counts, OperationStatus.Completed),
            Count(counts, OperationStatus.Failed) + Count(counts, OperationStatus.DeadLetter),
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

    private static int Count(IReadOnlyDictionary<OperationStatus, int> counts, OperationStatus status) => counts.GetValueOrDefault(status);

    private static QueuedOperationView ViewOf(Operation operation, string provider, CanonicalItemState? state)
        => new(operation.Id.Value, provider, operation.Type.ToString(), operation.Status.ToString(), OperationLookup.PathOf(state), OperationLookup.SizeOf(state), operation.CreatedUtc);

    private static Fail<IpcError> Failure(StoreError error) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", "operation queue")));
}
