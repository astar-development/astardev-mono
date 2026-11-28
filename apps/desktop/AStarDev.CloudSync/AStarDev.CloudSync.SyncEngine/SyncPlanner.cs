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
    private const int BatchSize = 500;

    private static readonly Dictionary<ConflictReason, ConflictType> ConflictTypes = new()
    {
        [ConflictReason.CreateCreate] = ConflictType.CreateCreate,
        [ConflictReason.ModifyModify] = ConflictType.ModifyModify,
        [ConflictReason.RenameRename] = ConflictType.RenameRename,
        [ConflictReason.KeepDelete] = ConflictType.KeepDelete
    };

    private readonly ISyncJobStore jobStore;
    private readonly IItemStateStore itemStore;
    private readonly ISyncScopeStore scopeStore;
    private readonly IKnownSnapshotStore knownStore;
    private readonly IOperationStore operationStore;
    private readonly IConflictStore conflictStore;
    private readonly IPlanStore planStore;
    private readonly IConflictEventSink conflictEvents;
    private readonly IReconciler reconciler = new Reconciler();
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SyncPlanner> logger;

    internal SyncPlanner(ISyncJobStore jobStore, ISyncScopeStore scopeStore, IItemStateStore itemStore, IKnownSnapshotStore knownStore, IOperationStore operationStore, IConflictStore conflictStore, IPlanStore planStore, IConflictEventSink conflictEvents, TimeProvider timeProvider, ILogger<SyncPlanner> logger)
    {
        this.planStore = planStore;
        this.conflictEvents = conflictEvents;
        this.jobStore = jobStore;
        this.scopeStore = scopeStore;
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
    /// Items are written in batches of up to 500 through <see cref="IPlanStore"/>, one transaction each, so a store error leaves the failing batch unwritten and earlier batches queued; the open conflict gauge is refreshed once per plan.
    /// </summary>
    /// <param name="jobId">The job to plan. Only items inside one of its scopes are planned: an item outside every scope is never uploaded and never deleted locally, so nothing happens without an explicit scope, and removing a scope leaves its local files alone. Its stored bootstrap mode governs only the first plan, while the job has no baseline and no operation: Download copies remote content over local, Upload copies local over remote, and neither deletes the other side's extra items. Merge, and every later plan, use the normal rules, so differing content on both sides is a conflict.</param>
    /// <param name="cancellationToken">Cancelling stops planning and surfaces as <see cref="OperationCanceledException"/>; batches already written stay queued and the batch being planned is not written.</param>
    /// <returns>How many operations were queued and conflicts stored, or the <see cref="StoreError"/> that stopped planning, including <see cref="SyncJobNotFound"/> when the job does not exist.</returns>
    public async Task<Result<PlanSummary, StoreError>> PlanAsync(SyncJobId jobId, CancellationToken cancellationToken)
    {
        var reconcileStarted = timeProvider.GetTimestamp();
        using var span = WorkflowTelemetry.StartPlan(jobId);
        using var scope = CorrelationScope.Begin(logger, syncJobId: jobId.Value.ToString());
        var outcome = await ThenAsync(jobStore.GetAsync(jobId, cancellationToken), job =>
            ThenAsync(scopeStore.ListAsync(jobId, cancellationToken), scopes =>
                ThenAsync(itemStore.ListAsync(jobId, cancellationToken), rows =>
                    ThenAsync(knownStore.ListAsync(jobId, cancellationToken), known =>
                        ThenAsync(operationStore.ListAsync(cancellationToken), existing => PlanAllAsync(jobId, BootstrapModeFor(job, known, existing), InScope(CanonicalStateBuilder.Build(rows, known), scopes), BusyItems(existing), cancellationToken))))));
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

    private static List<CanonicalItemState> InScope(IReadOnlyList<CanonicalItemState> states, IReadOnlyList<SyncScope> scopes)
        => states.Where(state => PathOf(state) is { } path && ScopeCoverage.Covers(scopes, path)).ToList();

    private static RelativePath? PathOf(CanonicalItemState state) => state.Remote?.Path ?? state.Local?.Path ?? state.Known?.Path;

    internal static HashSet<ItemId> BusyItems(IReadOnlyList<Operation> existing)
        => existing.Where(operation => operation.Status is OperationStatus.Pending or OperationStatus.Running or OperationStatus.DeadLetter).Select(operation => operation.ItemId).ToHashSet();

    private static BootstrapMode BootstrapModeFor(SyncJob job, IReadOnlyList<KnownEntry> known, IReadOnlyList<Operation> existing)
        => known.Count == 0 && existing.All(operation => operation.SyncJobId != job.Id) ? job.BootstrapMode : BootstrapMode.Merge;

    private static ConflictType ToConflictType(ConflictReason reason) => ConflictTypes[reason];

    private async Task<Result<PlanSummary, StoreError>> PlanAllAsync(SyncJobId jobId, BootstrapMode mode, IReadOnlyList<CanonicalItemState> states, HashSet<ItemId> busy, CancellationToken cancellationToken)
    {
        var total = new PlanSummary(0, 0);
        Result<PlanSummary, StoreError> outcome = new Ok<PlanSummary>(total);

        foreach (var chunk in states.Chunk(BatchSize))
        {
            var saved = await SaveAsync(BatchOf(jobId, mode, chunk, busy, cancellationToken), cancellationToken);
            if (saved.Value is Fail<StoreError>)
            {
                outcome = saved;
                break;
            }

            var summary = ((Ok<PlanSummary>)saved.Value).Value;
            total = new PlanSummary(total.Queued + summary.Queued, total.Conflicts + summary.Conflicts);
            outcome = new Ok<PlanSummary>(total);
        }

        if (total.Conflicts > 0) await SyncMetrics.Shared.RefreshOpenConflictsAsync(conflictStore, cancellationToken);

        return outcome;
    }

    private PlanBatch BatchOf(SyncJobId jobId, BootstrapMode mode, IReadOnlyList<CanonicalItemState> states, HashSet<ItemId> busy, CancellationToken cancellationToken)
    {
        var forgotten = new List<ItemId>();
        var work = new List<PlannedWork>();
        var conflicts = new List<PlannedConflict>();

        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var item = WorkflowTelemetry.StartItemPlan(jobId, state.ItemId);
            if (IsGone(state, busy))
            {
                forgotten.Add(state.ItemId);
                continue;
            }

            switch (Decide(mode, state))
            {
                case Resolved resolved:
                    work.AddRange(WorkFor(jobId, state, resolved, busy, null));
                    break;
                case Conflicted when busy.Contains(state.ItemId):
                    break;
                case Conflicted conflicted:
                    conflicts.Add(ConflictFor(jobId, state, conflicted));
                    break;
            }
        }

        return new PlanBatch(jobId, forgotten, work, conflicts);
    }

    private static bool IsGone(CanonicalItemState state, HashSet<ItemId> busy) => state is { Known: not null, Local: null, Remote: null } && !busy.Contains(state.ItemId);

    private SyncDecision Decide(BootstrapMode mode, CanonicalItemState state)
    {
        using var span = WorkflowTelemetry.StartReconcile();

        return mode != BootstrapMode.Merge && state.Known is null ? BootstrapResolver.Resolve(mode, state) : reconciler.Reconcile(state);
    }

    internal async Task<Result<PlanSummary, StoreError>> QueueAsync(SyncJobId jobId, CanonicalItemState state, Resolved decision, HashSet<ItemId> busy, OperationPriority? priority, CancellationToken cancellationToken)
        => await SaveAsync(new PlanBatch(jobId, [], WorkFor(jobId, state, decision, busy, priority), []), cancellationToken);

    private IReadOnlyList<PlannedWork> WorkFor(SyncJobId jobId, CanonicalItemState state, Resolved decision, HashSet<ItemId> busy, OperationPriority? priority)
    {
        var plan = OperationPlanner.Plan(state, decision);
        if (plan.Operations.Count == 0 || busy.Contains(state.ItemId)) return [];

        using var planning = WorkflowTelemetry.StartOperationPlan(jobId, state.ItemId);
        var now = timeProvider.GetUtcNow();
        var operations = plan.Operations
            .Select((planned, index) => new Operation(new OperationId(Guid.NewGuid()), jobId, state.ItemId, planned.Type, priority ?? planned.Priority, OperationStatus.Pending, 0, now, null, now.AddMilliseconds(index), planning?.Id))
            .ToList();
        foreach (var operation in operations) WorkflowTelemetry.StartPublish(operation)?.Dispose();
        busy.Add(state.ItemId);

        return [new PlannedWork(operations, plan.Known)];
    }

    private PlannedConflict ConflictFor(SyncJobId jobId, CanonicalItemState state, Conflicted decision)
    {
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), jobId, state.ItemId, ToConflictType(decision.Reason), ConflictStatus.Open, timeProvider.GetUtcNow());
        var payload = JsonSerializer.Serialize(new { conflictId = conflict.Id.Value, syncJobId = conflict.SyncJobId.Value, itemId = conflict.ItemId.Value, type = conflict.Type.ToString() });

        return new PlannedConflict(conflict, new JournalEntry(Guid.NewGuid(), conflict.CreatedUtc, ConflictDetectedEvent, conflict.Id.Value, payload));
    }

    private async Task<Result<PlanSummary, StoreError>> SaveAsync(PlanBatch batch, CancellationToken cancellationToken)
        => batch.IsEmpty ? new Ok<PlanSummary>(new PlanSummary(0, 0)) : await planStore.SaveAsync(batch, cancellationToken) switch
        {
            Ok<IReadOnlyList<ConflictId>> stored => await AnnounceAsync(batch, stored.Value.ToHashSet(), cancellationToken),
            Fail<StoreError> fail => fail
        };

    private async Task<Result<PlanSummary, StoreError>> AnnounceAsync(PlanBatch batch, HashSet<ConflictId> stored, CancellationToken cancellationToken)
    {
        var operations = batch.Work.SelectMany(work => work.Operations).ToList();
        foreach (var operation in operations) OperationLog.Queued(logger, operation.Id.Value, operation.Type.ToString(), operation.ItemId.Value, batch.JobId.Value);

        var detected = batch.Conflicts.Select(planned => planned.Conflict).Where(conflict => stored.Contains(conflict.Id)).ToList();
        foreach (var conflict in detected)
        {
            SyncLog.ConflictDetected(logger, conflict.Id.Value, conflict.ItemId.Value, conflict.SyncJobId.Value, conflict.Type.ToString());
            await conflictEvents.DetectedAsync(conflict, cancellationToken);
        }

        return new Ok<PlanSummary>(new PlanSummary(operations.Count, detected.Count));
    }
}
