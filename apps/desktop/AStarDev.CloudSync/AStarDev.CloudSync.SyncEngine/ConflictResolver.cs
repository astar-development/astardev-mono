using System.Text.Json;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// Applies the user's decision for conflicts. A decision becomes durable operations through <see cref="SyncPlanner"/>, so executors still do the work and the baseline of an item still moves only when its operation succeeds.
/// Every decision is journaled, and every resolution is announced to the <see cref="IConflictEventSink"/>. Nothing is discarded unless the user chose the side that no longer has the file.
/// </summary>
public sealed class ConflictResolver
{
    private const string ResolvedEvent = "ConflictResolved";
    private const string DeferredEvent = "ConflictDeferred";

    private readonly IConflictStore conflictStore;
    private readonly ISyncJobStore jobStore;
    private readonly IItemStateStore itemStore;
    private readonly IKnownSnapshotStore knownStore;
    private readonly IOperationStore operationStore;
    private readonly ILocalObservationStore observations;
    private readonly ILocalFileStore files;
    private readonly IEventJournal journal;
    private readonly IConflictEventSink conflictEvents;
    private readonly SyncPlanner planner;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<ConflictResolver> logger;

    internal ConflictResolver(IConflictStore conflictStore, ISyncJobStore jobStore, IItemStateStore itemStore, IKnownSnapshotStore knownStore, IOperationStore operationStore, ILocalObservationStore observations, ILocalFileStore files, IEventJournal journal, IConflictEventSink conflictEvents, SyncPlanner planner, TimeProvider timeProvider, ILogger<ConflictResolver> logger)
    {
        this.conflictStore = conflictStore;
        this.jobStore = jobStore;
        this.itemStore = itemStore;
        this.knownStore = knownStore;
        this.operationStore = operationStore;
        this.observations = observations;
        this.files = files;
        this.journal = journal;
        this.conflictEvents = conflictEvents;
        this.planner = planner;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Applies one choice to each conflict, in order; one that cannot be resolved does not stop the rest. Keep local, keep remote and keep both queue operations at <see cref="OperationPriority.UserInitiated"/>, mark the conflict
    /// <see cref="ConflictStatus.Resolved"/>, journal <c>ConflictResolved</c> and announce it. Keep both first copies the local file to <c>&lt;name&gt; (Conflict - yyyy-MM-dd HHmmss)&lt;ext&gt;</c>, with a numeric suffix on a clash,
    /// and records the copy as a new local item that the planner uploads. Defer marks the conflict <see cref="ConflictStatus.Deferred"/> and changes nothing about the item, journals <c>ConflictDeferred</c> and announces nothing, because the conflict stays listed and can still be resolved.
    /// </summary>
    /// <param name="conflictIds">The conflicts to resolve. Empty resolves nothing.</param>
    /// <param name="choice">The one choice applied to every conflict.</param>
    /// <param name="cancellationToken">Cancelling stops before the next conflict and surfaces as <see cref="OperationCanceledException"/>; conflicts already resolved stay resolved.</param>
    /// <returns>One result per id, in the order given: success, or why that conflict was not resolved, for example <see cref="ConflictNotOpen"/> for one that was already resolved. An open or a deferred conflict can be resolved.</returns>
    public async Task<IReadOnlyList<Result<Unit, ResolutionError>>> ResolveAsync(IReadOnlyList<ConflictId> conflictIds, ConflictResolution choice, CancellationToken cancellationToken)
    {
        var outcomes = new List<Result<Unit, ResolutionError>>(conflictIds.Count);

        foreach (var id in conflictIds) outcomes.Add(await ResolveOneAsync(id, choice, cancellationToken));

        return outcomes;
    }

    private static Fail<ResolutionError> Stored(StoreError error) => new(new ResolutionStoreFailed(error));

    private static Fail<ResolutionError> Refusal(ConflictItemGone reason) => new(reason);

    private static Fail<ResolutionError> Refusal(ConflictItemBusy reason) => new(reason);

    private static Fail<ResolutionError> Refusal(KeepBothCopyFailed reason) => new(reason);

    private async Task<Result<Unit, ResolutionError>> ResolveOneAsync(ConflictId id, ConflictResolution choice, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await conflictStore.GetAsync(id, cancellationToken) switch
        {
            Ok<Conflict> { Value.Status: ConflictStatus.Open or ConflictStatus.Deferred } open => await ApplyAsync(open.Value, choice, cancellationToken),
            Ok<Conflict> => new Fail<ResolutionError>(new ConflictNotOpen(id)),
            Fail<StoreError> fail => Stored(fail.Error)
        };
    }

    private async Task<Result<Unit, ResolutionError>> ApplyAsync(Conflict conflict, ConflictResolution choice, CancellationToken cancellationToken)
    {
        if (choice == ConflictResolution.Defer) return await DeferAsync(conflict, cancellationToken);

        var loaded = await LoadAsync(conflict, cancellationToken);
        if (loaded.Value is Fail<ResolutionError> failure) return failure;

        var context = ((Ok<Loaded>)loaded.Value).Value;
        RelativePath? copy = null;

        if (choice == ConflictResolution.KeepBoth && ResolutionPlanner.NeedsCopy(context.State))
        {
            var copied = await CopyLocalAsync(conflict, context, cancellationToken);
            if (copied.Value is Fail<ResolutionError> copyFailure) return copyFailure;

            copy = ((Ok<RelativePath>)copied.Value).Value;
        }

        var steps = ResolutionPlanner.Plan(conflict.Type, context.State, choice);
        if (await planner.QueueAsync(conflict.SyncJobId, context.State, steps, [], OperationPriority.UserInitiated, cancellationToken) is { Value: Fail<StoreError> queueFailure }) return Stored(queueFailure.Error);

        var status = await conflictStore.UpdateStatusAsync(conflict.Id, ConflictStatus.Resolved, cancellationToken);
        if (status.Value is Fail<StoreError> statusFailure) return Stored(statusFailure.Error);

        SyncMetrics.Shared.ConflictResolved(choice.ToString());
        SyncLog.ConflictResolved(logger, conflict.Id.Value, conflict.ItemId.Value, conflict.SyncJobId.Value, choice.ToString());
        var recorded = await RecordAsync(conflict with { Status = ConflictStatus.Resolved }, choice, copy, cancellationToken);
        if (copy is not null) await ReplanAsync(conflict.SyncJobId, cancellationToken);

        return recorded;
    }

    private async Task<Result<Unit, ResolutionError>> DeferAsync(Conflict conflict, CancellationToken cancellationToken)
    {
        var status = await conflictStore.UpdateStatusAsync(conflict.Id, ConflictStatus.Deferred, cancellationToken);

        return (status.Value is Fail<StoreError> statusFailure)
            ? Stored(statusFailure.Error)
            : await RecordAsync(conflict with { Status = ConflictStatus.Deferred }, ConflictResolution.Defer, null, cancellationToken);
    }

    private async Task<Result<Loaded, ResolutionError>> LoadAsync(Conflict conflict, CancellationToken cancellationToken)
    {
        var job = await jobStore.GetAsync(conflict.SyncJobId, cancellationToken);
        if (job.Value is Fail<StoreError> jobFailure) return Stored(jobFailure.Error);

        var rows = await itemStore.ListAsync(conflict.SyncJobId, cancellationToken);
        if (rows.Value is Fail<StoreError> rowsFailure) return Stored(rowsFailure.Error);

        var known = await knownStore.ListAsync(conflict.SyncJobId, cancellationToken);
        if (known.Value is Fail<StoreError> knownFailure) return Stored(knownFailure.Error);

        var operations = await operationStore.ListAsync(cancellationToken);
        if (operations.Value is Fail<StoreError> operationsFailure) return Stored(operationsFailure.Error);

        var items = ((Ok<IReadOnlyList<StoredItem>>)rows.Value).Value;
        var state = CanonicalStateBuilder.Build(items, ((Ok<IReadOnlyList<KnownEntry>>)known.Value).Value).FirstOrDefault(candidate => candidate.ItemId == conflict.ItemId);
        if (state is null) return Refusal(new ConflictItemGone(conflict.Id));

        return IsBusy(conflict, operations)
            ? Refusal(new ConflictItemBusy(conflict.Id))
            : new Ok<Loaded>(new Loaded(((Ok<SyncJob>)job.Value).Value, state, items));
    }

    private static bool IsBusy(Conflict conflict, Result<IReadOnlyList<Operation>, StoreError> operations) =>
        SyncPlanner.BusyItems(((Ok<IReadOnlyList<Operation>>)operations.Value).Value).Contains(conflict.ItemId);

    private async Task<Result<RelativePath, ResolutionError>> CopyLocalAsync(Conflict conflict, Loaded context, CancellationToken cancellationToken)
    {
        var local = context.State.Local!;
        var root = context.Job.LocalRootPath;
        var taken = context.Items.Select(item => item.Path).ToHashSet();
        var target = KeepBoth.NameCopy(local.Path, timeProvider.GetUtcNow(), path => taken.Contains(path) || files.Exists(root, path));

        return files.OpenRead(root, local.Path) switch
        {
            Ok<Stream> content => await WriteCopyAsync(conflict, context, local, target, content.Value, cancellationToken),
            Fail<ProviderError> fail => Refusal(new KeepBothCopyFailed(conflict.Id, Describe(fail.Error)))
        };
    }

    private async Task<Result<RelativePath, ResolutionError>> WriteCopyAsync(Conflict conflict, Loaded context, LocalSnapshot local, RelativePath target, Stream content, CancellationToken cancellationToken)
    {
        Result<Unit, ProviderError> written;
        await using (content.ConfigureAwait(false)) written = await files.WriteAsync(context.Job.LocalRootPath, target, content, local.ModifiedUtc, cancellationToken);
        if (written.Value is Fail<ProviderError> writeFailure) return Refusal(new KeepBothCopyFailed(conflict.Id, Describe(writeFailure.Error)));

        var snapshot = new LocalSnapshot(target, local.Size, local.ModifiedUtc, null, local.Fingerprint);
        var recorded = await observations.RecordAsync(conflict.SyncJobId, [snapshot], [], timeProvider.GetUtcNow(), cancellationToken);

        return recorded.Value is Fail<StoreError> recordFailure ? Stored(recordFailure.Error) : new Ok<RelativePath>(target);
    }

    private static string Describe(ProviderError error) => error.Value?.ToString() ?? "unknown";

    private async Task<Result<Unit, ResolutionError>> RecordAsync(Conflict conflict, ConflictResolution choice, RelativePath? copy, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var payload = JsonSerializer.Serialize(new { conflictId = conflict.Id.Value, syncJobId = conflict.SyncJobId.Value, itemId = conflict.ItemId.Value, type = conflict.Type.ToString(), resolution = choice.ToString(), copyPath = copy?.Value });
        var entry = new JournalEntry(Guid.NewGuid(), now, choice == ConflictResolution.Defer ? DeferredEvent : ResolvedEvent, conflict.Id.Value, payload);
        if (await journal.AppendAsync(entry, cancellationToken) is { Value: Fail<StoreError> failure }) return Stored(failure.Error);

        if (choice != ConflictResolution.Defer) await conflictEvents.ResolvedAsync(conflict, choice.ToString(), now, cancellationToken);

        return new Ok<Unit>(default);
    }

    private async Task ReplanAsync(SyncJobId jobId, CancellationToken cancellationToken)
    {
        if (await planner.PlanAsync(jobId, cancellationToken) is { Value: Fail<StoreError> failure }) logger.LogWarning("Planning job {Job} after keep both failed: {Error}", jobId.Value, failure.Error.Value);
    }

    private sealed record Loaded(SyncJob Job, CanonicalItemState State, IReadOnlyList<StoredItem> Items);
}
