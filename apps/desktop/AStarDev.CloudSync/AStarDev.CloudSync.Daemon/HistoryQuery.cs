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
    private static readonly OperationStatus[] Finished = [OperationStatus.Completed, OperationStatus.Failed, OperationStatus.DeadLetter];

    /// <summary>Lists the newest operations that completed or failed, at most <see cref="ApplicationConstants.HistoryListLimit"/>, newest first, narrowed by every filter the query sets, so one answer always fits an IPC frame however long the history is.</summary>
    /// <returns>
    /// The matching operations, empty when none match. An operation that gave up after its retries counts as failed. An operation whose item or account the daemon no longer knows keeps an empty path or provider and fails the path or provider filter when that filter is set.
    /// <see cref="HandlerFailed"/> carries a store failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public Task<Result<HistoryView, IpcError>> HandleAsync(GetHistory query, CancellationToken cancellationToken) => ListAsync(query, ApplicationConstants.HistoryListLimit, cancellationToken);

    /// <summary>Lists every operation <see cref="HandleAsync"/> would, without its limit, for an export that is written to a file and never sent as one IPC frame.</summary>
    /// <returns>The same results and failures as <see cref="HandleAsync"/>.</returns>
    public Task<Result<HistoryView, IpcError>> ListAllAsync(GetHistory query, CancellationToken cancellationToken) => ListAsync(query, int.MaxValue, cancellationToken);

    private async Task<Result<HistoryView, IpcError>> ListAsync(GetHistory query, int limit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await operations.ListNewestAsync(StatusesOf(query), IsNarrowedAfterRead(query) ? int.MaxValue : limit, cancellationToken) switch
        {
            Ok<IReadOnlyList<Operation>> ok => await DescribeAsync(query, limit, ok.Value.Where(operation => Keeps(query, operation)).ToList(), cancellationToken),
            Fail<StoreError> fail => Failure(fail.Error)
        };
    }

    private async Task<Result<HistoryView, IpcError>> DescribeAsync(GetHistory query, int limit, IReadOnlyList<Operation> finished, CancellationToken cancellationToken)
    {
        if (finished.Count == 0) return new Ok<HistoryView>(new HistoryView([]));

        var providers = await lookup.ProvidersAsync(cancellationToken);
        if (providers.Value is Fail<StoreError> providersFailure) return Failure(providersFailure.Error);

        var items = await lookup.ItemsAsync(finished.Select(operation => operation.SyncJobId).Distinct().ToList(), cancellationToken);
        if (items.Value is Fail<StoreError> itemsFailure) return Failure(itemsFailure.Error);

        var providerOf = ((Ok<IReadOnlyDictionary<SyncJobId, string>>)providers.Value).Value;
        var stateOf = ((Ok<IReadOnlyDictionary<ItemId, CanonicalItemState>>)items.Value).Value;
        var entries = finished.Select(operation => EntryOf(operation, providerOf.GetValueOrDefault(operation.SyncJobId, string.Empty), stateOf.GetValueOrDefault(operation.ItemId))).Where(entry => Matches(query, entry)).Take(limit).ToList();

        return new Ok<HistoryView>(new HistoryView(entries));
    }

    private static OperationStatus[] StatusesOf(GetHistory query)
        => Option.FromNullable(query.Status).Match(
            status => Finished.Where(finished => string.Equals(status, OutcomeOf(finished), StringComparison.OrdinalIgnoreCase)).ToArray(),
            () => Finished);

    private static bool IsNarrowedAfterRead(GetHistory query)
        => query.Type is not null || query.From is not null || query.To is not null || query.Provider is not null || Option.FromNullable(query.Path).Bind(NonBlank).Value is Some<string>;

    private static bool Keeps(GetHistory query, Operation operation)
        => Passes(Option.FromNullable(query.Type), type => string.Equals(type, operation.Type.ToString(), StringComparison.OrdinalIgnoreCase))
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
