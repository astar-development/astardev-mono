using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetHistory"/> from the <see cref="IOperationStore"/>, the job and account stores and the item state.</summary>
internal sealed class HistoryQuery(IOperationStore operations, OperationLookup lookup)
{
    private const string Completed = "Completed";
    private const string Failed = "Failed";

    /// <summary>Lists the operations that completed or failed, newest first, narrowed by every filter the query sets.</summary>
    /// <returns>
    /// The matching operations, empty when none match. An operation that gave up after its retries counts as failed. An operation whose item or account the daemon no longer knows keeps an empty path or provider and fails the path or provider filter when that filter is set.
    /// <see cref="HandlerFailed"/> carries a store failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<HistoryView, IpcError>> HandleAsync(GetHistory query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await operations.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<Operation>> ok => await DescribeAsync(query, ok.Value.Where(operation => IsFinished(operation) && Keeps(query, operation)).OrderByDescending(operation => operation.CreatedUtc).ToList(), cancellationToken),
            Fail<StoreError> fail => Failure(fail.Error)
        };
    }

    private async Task<Result<HistoryView, IpcError>> DescribeAsync(GetHistory query, IReadOnlyList<Operation> finished, CancellationToken cancellationToken)
    {
        if (finished.Count == 0) return new Ok<HistoryView>(new HistoryView([]));

        var providers = await lookup.ProvidersAsync(cancellationToken);
        if (providers.Value is Fail<StoreError> providersFailure) return Failure(providersFailure.Error);

        var items = await lookup.ItemsAsync(finished.Select(operation => operation.SyncJobId).Distinct().ToList(), cancellationToken);
        if (items.Value is Fail<StoreError> itemsFailure) return Failure(itemsFailure.Error);

        var providerOf = ((Ok<IReadOnlyDictionary<SyncJobId, string>>)providers.Value).Value;
        var stateOf = ((Ok<IReadOnlyDictionary<ItemId, CanonicalItemState>>)items.Value).Value;
        var entries = finished.Select(operation => EntryOf(operation, providerOf.GetValueOrDefault(operation.SyncJobId, string.Empty), stateOf.GetValueOrDefault(operation.ItemId))).Where(entry => Matches(query, entry)).ToList();

        return new Ok<HistoryView>(new HistoryView(entries));
    }

    private static bool IsFinished(Operation operation) => operation.Status is OperationStatus.Completed or OperationStatus.Failed or OperationStatus.DeadLetter;

    private static bool Keeps(GetHistory query, Operation operation)
        => Passes(Option.FromNullable(query.Type), type => string.Equals(type, operation.Type.ToString(), StringComparison.OrdinalIgnoreCase))
            && Passes(Option.FromNullable(query.Status), status => string.Equals(status, OutcomeOf(operation.Status), StringComparison.OrdinalIgnoreCase))
            && Passes(Option.FromNullable(query.From), from => operation.CreatedUtc >= from)
            && Passes(Option.FromNullable(query.To), to => operation.CreatedUtc <= to);

    private static bool Matches(GetHistory query, HistoryEntryView entry)
        => Passes(Option.FromNullable(query.Provider), provider => string.Equals(provider, entry.Provider, StringComparison.OrdinalIgnoreCase))
            && Passes(Option.FromNullable(query.Path).Bind(NonBlank), path => entry.Path.Contains(path, StringComparison.OrdinalIgnoreCase));

    private static bool Passes<T>(Option<T> filter, Func<T, bool> test) => filter.Match(test, () => true);

    private static Option<string> NonBlank(string text) => string.IsNullOrWhiteSpace(text) ? Option.None<string>() : Option.Some(text);

    private static string OutcomeOf(OperationStatus status) => status == OperationStatus.Completed ? Completed : Failed;

    private static HistoryEntryView EntryOf(Operation operation, string provider, CanonicalItemState? state)
        => new(operation.Id.Value, provider, operation.Type.ToString(), OutcomeOf(operation.Status), operation.CreatedUtc, OperationLookup.PathOf(state), OperationLookup.SizeOf(state));

    private static Fail<IpcError> Failure(StoreError error) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", "operation history")));
}
