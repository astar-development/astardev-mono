using AStarDev.CloudSync.Domain;
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
    private readonly IReconciler reconciler = new Reconciler();
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SyncPlanner> logger;

    internal SyncPlanner(ISyncJobStore jobStore, IItemStateStore itemStore, IKnownSnapshotStore knownStore, IOperationStore operationStore, IConflictStore conflictStore, TimeProvider timeProvider, ILogger<SyncPlanner> logger)
    {
        this.jobStore = jobStore;
        this.itemStore = itemStore;
        this.knownStore = knownStore;
        this.operationStore = operationStore;
        this.conflictStore = conflictStore;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Plans the work for every item of the job. An item that already has a pending, running or dead-lettered operation is left alone, so planning again never queues the same work twice;
    /// an item in conflict gets one open conflict per reason however often it is planned.
    /// </summary>
    /// <param name="jobId">The job to plan. Its stored bootstrap mode governs only the first plan, while the job has no baseline and no operation: Download copies remote content over local, Upload copies local over remote, and neither deletes the other side's extra items. Merge, and every later plan, use the normal rules, so differing content on both sides is a conflict.</param>
    /// <param name="cancellationToken">Cancelling stops planning and surfaces as <see cref="OperationCanceledException"/>; what was already queued stays queued.</param>
    /// <returns>How many operations were queued and conflicts stored, or the <see cref="StoreError"/> that stopped planning, including <see cref="SyncJobNotFound"/> when the job does not exist.</returns>
    public async Task<Result<PlanSummary, StoreError>> PlanAsync(SyncJobId jobId, CancellationToken cancellationToken)
    {
        var outcome = await ThenAsync(jobStore.GetAsync(jobId, cancellationToken), job =>
            ThenAsync(itemStore.ListAsync(jobId, cancellationToken), rows =>
                ThenAsync(knownStore.ListAsync(jobId, cancellationToken), known =>
                    ThenAsync(operationStore.ListAsync(cancellationToken), existing => PlanAllAsync(jobId, BootstrapModeFor(job, known, existing), CanonicalStateBuilder.Build(rows, known), BusyItems(existing), cancellationToken)))));
        if (outcome.Value is Ok<PlanSummary> ok) logger.LogInformation("Planned job {Job}: {Queued} operations queued, {Conflicts} conflicts stored", jobId.Value, ok.Value.Queued, ok.Value.Conflicts);

        return outcome;
    }

    private static async Task<Result<TOut, StoreError>> ThenAsync<TIn, TOut>(Task<Result<TIn, StoreError>> pending, Func<TIn, Task<Result<TOut, StoreError>>> next)
        => await pending switch
        {
            Ok<TIn> ok => await next(ok.Value),
            Fail<StoreError> fail => fail
        };

    private static HashSet<ItemId> BusyItems(IReadOnlyList<Operation> existing)
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
            var outcome = Decide(mode, state) switch
            {
                Resolved resolved => await QueueAsync(jobId, state, resolved, busy, cancellationToken),
                Conflicted conflicted => await RecordConflictAsync(jobId, state, conflicted, cancellationToken)
            };
            if (outcome.Value is Fail<StoreError> failure) return failure;
            if (outcome.Value is Ok<PlanSummary> ok) total = new PlanSummary(total.Queued + ok.Value.Queued, total.Conflicts + ok.Value.Conflicts);
        }

        return new Ok<PlanSummary>(total);
    }

    private SyncDecision Decide(BootstrapMode mode, CanonicalItemState state)
        => mode != BootstrapMode.Merge && state.Known is null ? BootstrapResolver.Resolve(mode, state) : reconciler.Reconcile(state);

    private async Task<Result<PlanSummary, StoreError>> QueueAsync(SyncJobId jobId, CanonicalItemState state, Resolved decision, HashSet<ItemId> busy, CancellationToken cancellationToken)
    {
        var plan = OperationPlanner.Plan(state, decision);
        if (plan.Operations.Count == 0 || busy.Contains(state.ItemId)) return new Ok<PlanSummary>(new PlanSummary(0, 0));

        var now = timeProvider.GetUtcNow();
        var operations = plan.Operations
            .Select((planned, index) => new Operation(new OperationId(Guid.NewGuid()), jobId, state.ItemId, planned.Type, planned.Priority, OperationStatus.Pending, 0, now, null, now.AddTicks(index)))
            .ToList();

        var staged = await knownStore.StageAsync(jobId, operations[^1].Id, state.ItemId, plan.Known, cancellationToken);
        if (staged.Value is Fail<StoreError> stageFailure) return stageFailure;

        foreach (var operation in operations)
        {
            var enqueued = await operationStore.EnqueueAsync(operation, cancellationToken);
            if (enqueued.Value is Fail<StoreError> enqueueFailure) return enqueueFailure;
        }

        busy.Add(state.ItemId);

        return new Ok<PlanSummary>(new PlanSummary(operations.Count, 0));
    }

    private async Task<Result<PlanSummary, StoreError>> RecordConflictAsync(SyncJobId jobId, CanonicalItemState state, Conflicted decision, CancellationToken cancellationToken)
    {
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), jobId, state.ItemId, ToConflictType(decision.Reason), ConflictStatus.Open, timeProvider.GetUtcNow());

        return await conflictStore.AddAsync(conflict, cancellationToken) switch
        {
            Ok<bool> ok => new Ok<PlanSummary>(new PlanSummary(0, ok.Value ? 1 : 0)),
            Fail<StoreError> fail => fail
        };
    }
}
