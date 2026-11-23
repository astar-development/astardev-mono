using System.Text.Json;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// Reconciles every item of a job and turns the decisions into durable work: operations on the queue and conflicts for the user.
/// It decides and plans only; executors do the work, and the baseline of an item moves only when its last operation succeeds.
/// </summary>
public sealed class SyncPlanner
{
    private const string ConflictDetectedEvent = "ConflictDetected";

    private static readonly Dictionary<ConflictReason, ConflictType> ConflictTypes = new()
    {
        [ConflictReason.CreateCreate] = ConflictType.CreateCreate,
        [ConflictReason.ModifyModify] = ConflictType.ModifyModify,
        [ConflictReason.RenameRename] = ConflictType.RenameRename,
        [ConflictReason.KeepDelete] = ConflictType.KeepDelete
    };

    private readonly ISyncJobStore jobStore;
    private readonly IItemStateStore itemStore;
    private readonly IKnownSnapshotStore knownStore;
    private readonly IOperationStore operationStore;
    private readonly IConflictStore conflictStore;
    private readonly IEventJournal journal;
    private readonly IConflictEventSink conflictEvents;
    private readonly IReconciler reconciler = new Reconciler();
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SyncPlanner> logger;

    internal SyncPlanner(ISyncJobStore jobStore, IItemStateStore itemStore, IKnownSnapshotStore knownStore, IOperationStore operationStore, IConflictStore conflictStore, IEventJournal journal, IConflictEventSink conflictEvents, TimeProvider timeProvider, ILogger<SyncPlanner> logger)
    {
        this.journal = journal;
        this.conflictEvents = conflictEvents;
        this.jobStore = jobStore;
        this.itemStore = itemStore;
        this.knownStore = knownStore;
        this.operationStore = operationStore;
        this.conflictStore = conflictStore;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Plans the work for every item of the job. An item gone from both sides loses its baseline and leftover rows, unless it still has such an operation, so the baselines do not pile up and a later item at the same path is new. An item that already has a pending, running or dead-lettered operation is left alone, so planning again never queues the same work twice;
    /// an item in conflict gets one open conflict per reason however often it is planned, and none while it has such work, which is how a resolved conflict is not raised again before its operations finish. A newly stored conflict is journaled as <c>ConflictDetected</c> and then announced to the <see cref="IConflictEventSink"/>.
    /// </summary>
    /// <param name="jobId">The job to plan. Its stored bootstrap mode governs only the first plan, while the job has no baseline and no operation: Download copies remote content over local, Upload copies local over remote, and neither deletes the other side's extra items. Merge, and every later plan, use the normal rules, so differing content on both sides is a conflict.</param>
    /// <param name="cancellationToken">Cancelling stops planning and surfaces as <see cref="OperationCanceledException"/>; what was already queued stays queued.</param>
    /// <returns>How many operations were queued and conflicts stored, or the <see cref="StoreError"/> that stopped planning, including <see cref="SyncJobNotFound"/> when the job does not exist.</returns>
    public async Task<Result<PlanSummary, StoreError>> PlanAsync(SyncJobId jobId, CancellationToken cancellationToken)
    {
        var reconcileStarted = timeProvider.GetTimestamp();
        using var span = WorkflowTelemetry.StartPlan(jobId);
        using var scope = CorrelationScope.Begin(logger, syncJobId: jobId.Value.ToString());
        var outcome = await ThenAsync(jobStore.GetAsync(jobId, cancellationToken), job =>
            ThenAsync(itemStore.ListAsync(jobId, cancellationToken), rows =>
                ThenAsync(knownStore.ListAsync(jobId, cancellationToken), known =>
                    ThenAsync(operationStore.ListAsync(cancellationToken), existing => PlanAllAsync(jobId, BootstrapModeFor(job, known, existing), CanonicalStateBuilder.Build(rows, known), BusyItems(existing), cancellationToken)))));
        PerformanceMetrics.Shared.RecordReconciliation(timeProvider.GetElapsedTime(reconcileStarted));
        if (outcome.Value is Fail<StoreError> failed) WorkflowTelemetry.Fail(span, failed.Error.Value?.GetType().Name ?? "unknown", "Planning stopped on a store error.");

        if (outcome.Value is Ok<PlanSummary> ok) EngineLog.JobPlanned(logger, jobId.Value, ok.Value.Queued, ok.Value.Conflicts);

        return outcome;
    }

    private static async Task<Result<TOut, StoreError>> ThenAsync<TIn, TOut>(Task<Result<TIn, StoreError>> pending, Func<TIn, Task<Result<TOut, StoreError>>> next)
        => await pending switch
        {
            Ok<TIn> ok => await next(ok.Value),
            Fail<StoreError> fail => fail
        };

    internal static HashSet<ItemId> BusyItems(IReadOnlyList<Operation> existing)
        => existing.Where(operation => operation.Status is OperationStatus.Pending or OperationStatus.Running or OperationStatus.DeadLetter).Select(operation => operation.ItemId).ToHashSet();

    private static BootstrapMode BootstrapModeFor(SyncJob job, IReadOnlyList<KnownEntry> known, IReadOnlyList<Operation> existing)
        => known.Count == 0 && existing.All(operation => operation.SyncJobId != job.Id) ? job.BootstrapMode : BootstrapMode.Merge;

    private static ConflictType ToConflictType(ConflictReason reason) => ConflictTypes[reason];

    private async Task<Result<PlanSummary, StoreError>> PlanAllAsync(SyncJobId jobId, BootstrapMode mode, IReadOnlyList<CanonicalItemState> states, HashSet<ItemId> busy, CancellationToken cancellationToken)
    {
        var total = new PlanSummary(0, 0);

        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var item = WorkflowTelemetry.StartItemPlan(jobId, state.ItemId);
            var outcome = IsGone(state, busy) ? await RemoveKnownAsync(jobId, state, cancellationToken) : Decide(mode, state) switch
            {
                Resolved resolved => await QueueAsync(jobId, state, resolved, busy, null, cancellationToken),
                Conflicted when busy.Contains(state.ItemId) => new Ok<PlanSummary>(new PlanSummary(0, 0)),
                Conflicted conflicted => await RecordConflictAsync(jobId, state, conflicted, cancellationToken)
            };
            if (outcome.Value is Fail<StoreError> failure)
            {
                WorkflowTelemetry.Fail(item, failure.Error.Value?.GetType().Name ?? "unknown", "Planning the item stopped on a store error.");

                return failure;
            }

            if (outcome.Value is Ok<PlanSummary> ok) total = new PlanSummary(total.Queued + ok.Value.Queued, total.Conflicts + ok.Value.Conflicts);
        }

        return new Ok<PlanSummary>(total);
    }

    private static bool IsGone(CanonicalItemState state, HashSet<ItemId> busy) => state is { Known: not null, Local: null, Remote: null } && !busy.Contains(state.ItemId);

    private async Task<Result<PlanSummary, StoreError>> RemoveKnownAsync(SyncJobId jobId, CanonicalItemState state, CancellationToken cancellationToken)
        => await knownStore.RemoveAsync(jobId, state.ItemId, cancellationToken) switch
        {
            Ok<Unit> => new Ok<PlanSummary>(new PlanSummary(0, 0)),
            Fail<StoreError> fail => fail
        };

    private SyncDecision Decide(BootstrapMode mode, CanonicalItemState state)
    {
        using var span = WorkflowTelemetry.StartReconcile();

        return mode != BootstrapMode.Merge && state.Known is null ? BootstrapResolver.Resolve(mode, state) : reconciler.Reconcile(state);
    }

    internal async Task<Result<PlanSummary, StoreError>> QueueAsync(SyncJobId jobId, CanonicalItemState state, Resolved decision, HashSet<ItemId> busy, OperationPriority? priority, CancellationToken cancellationToken)
    {
        var plan = OperationPlanner.Plan(state, decision);
        if (plan.Operations.Count == 0 || busy.Contains(state.ItemId)) return new Ok<PlanSummary>(new PlanSummary(0, 0));

        using var planning = WorkflowTelemetry.StartOperationPlan(jobId, state.ItemId);
        var now = timeProvider.GetUtcNow();
        var operations = plan.Operations
            .Select((planned, index) => new Operation(new OperationId(Guid.NewGuid()), jobId, state.ItemId, planned.Type, priority ?? planned.Priority, OperationStatus.Pending, 0, now, null, now.AddTicks(index), planning?.Id))
            .ToList();

        var staged = await knownStore.StageAsync(jobId, operations[^1].Id, state.ItemId, plan.Known, cancellationToken);
        if (staged.Value is Fail<StoreError> stageFailure) return stageFailure;

        foreach (var operation in operations)
        {
            using var publish = WorkflowTelemetry.StartPublish(operation);
            var enqueued = await operationStore.EnqueueAsync(operation, cancellationToken);
            if (enqueued.Value is Fail<StoreError> enqueueFailure)
            {
                WorkflowTelemetry.Fail(publish, enqueueFailure.Error.Value?.GetType().Name ?? "unknown", "The operation could not be queued.");

                return enqueueFailure;
            }

            OperationLog.Queued(logger, operation.Id.Value, operation.Type.ToString(), operation.ItemId.Value, jobId.Value);
        }

        busy.Add(state.ItemId);

        return new Ok<PlanSummary>(new PlanSummary(operations.Count, 0));
    }

    private async Task<Result<PlanSummary, StoreError>> RecordConflictAsync(SyncJobId jobId, CanonicalItemState state, Conflicted decision, CancellationToken cancellationToken)
    {
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), jobId, state.ItemId, ToConflictType(decision.Reason), ConflictStatus.Open, timeProvider.GetUtcNow());

        return await conflictStore.AddAsync(conflict, cancellationToken) switch
        {
            Ok<bool> { Value: true } => await JournalAndAnnounceAsync(conflict, cancellationToken),
            Ok<bool> => new Ok<PlanSummary>(new PlanSummary(0, 0)),
            Fail<StoreError> fail => fail
        };
    }

    private async Task<Result<PlanSummary, StoreError>> JournalAndAnnounceAsync(Conflict conflict, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { conflictId = conflict.Id.Value, syncJobId = conflict.SyncJobId.Value, itemId = conflict.ItemId.Value, type = conflict.Type.ToString() });
        var entry = new JournalEntry(Guid.NewGuid(), conflict.CreatedUtc, ConflictDetectedEvent,conflict.Id.Value, payload);
        if (await journal.AppendAsync(entry, cancellationToken) is Fail<StoreError> failure) return failure;

        await SyncMetrics.Shared.RefreshOpenConflictsAsync(conflictStore, cancellationToken);
        SyncLog.ConflictDetected(logger, conflict.Id.Value, conflict.ItemId.Value, conflict.SyncJobId.Value, conflict.Type.ToString());
        await conflictEvents.DetectedAsync(conflict, cancellationToken);

        return new Ok<PlanSummary>(new PlanSummary(0, 1));
    }
}
