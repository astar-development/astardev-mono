using System.Diagnostics;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The spans that follow one logical operation from planning through the queue and the executor to the snapshot commit (doc 12). Every method returns <see langword="null"/> when nothing listens; dispose the span to end it.</summary>
internal static class WorkflowTelemetry
{
    private const string QueueName = "operations";
    private const string MessagingSystem = "cloudsync";

    /// <summary>Starts the span of one planning run of a job.</summary>
    public static Activity? StartPlan(SyncJobId syncJobId) => Tagged(SyncEngineTelemetry.Source.StartActivity("cloudsync.plan"), syncJobId);

    /// <summary>Starts a new trace for one item's planning, linked to the run that planned it, so each logical operation has its own trace.</summary>
    public static Activity? StartItemPlan(SyncJobId syncJobId, ItemId itemId)
    {
        var links = Activity.Current is { } run ? new[] { new ActivityLink(run.Context) } : null;
        var activity = SyncEngineTelemetry.Source.StartActivity("cloudsync.item.plan", ActivityKind.Internal, default(ActivityContext), links: links);
        activity?.SetTag("cloudsync.item.id", itemId.Value.ToString());

        return Tagged(activity, syncJobId);
    }

    /// <summary>Starts the span of the reconciliation decision for one item.</summary>
    public static Activity? StartReconcile() => SyncEngineTelemetry.Source.StartActivity("cloudsync.reconcile");

    /// <summary>Starts the span that plans the operations of one item. Its <see cref="Activity.Id"/> is what the operations carry as their trace parent.</summary>
    public static Activity? StartOperationPlan(SyncJobId syncJobId, ItemId itemId)
    {
        var activity = SyncEngineTelemetry.Source.StartActivity("cloudsync.operation.plan");
        activity?.SetTag("cloudsync.item.id", itemId.Value.ToString());

        return Tagged(activity, syncJobId);
    }

    /// <summary>Starts the span of putting one operation on the queue.</summary>
    public static Activity? StartPublish(Operation operation) => Messaging(SyncEngineTelemetry.Source.StartActivity("operations publish", ActivityKind.Producer), "publish", operation);

    /// <summary>Starts the span of executing one operation as a continuation of the trace its planner recorded; a new trace when it recorded none.</summary>
    public static Activity? StartExecute(Operation operation)
    {
        var parent = ActivityContext.TryParse(operation.TraceParent, null, true, out var context) ? context : default;
        var activity = Messaging(SyncEngineTelemetry.Source.StartActivity("operations process", ActivityKind.Consumer, parent), "process", operation);
        activity?.SetTag("cloudsync.operation.attempt", operation.Attempts);

        return activity;
    }

    /// <summary>Starts the span of applying the baseline of a succeeded operation.</summary>
    public static Activity? StartCommit(Operation operation) => Tagged(SyncEngineTelemetry.Source.StartActivity("cloudsync.snapshot.commit"), operation.SyncJobId);

    /// <summary>Starts the span of the provider request an operation makes.</summary>
    public static Activity? StartProviderRequest(Operation operation)
    {
        var activity = SyncEngineTelemetry.Source.StartActivity("cloudsync.provider.request", ActivityKind.Client);
        activity?.SetTag("cloudsync.operation.type", operation.Type.ToString());

        return Tagged(activity, operation.SyncJobId);
    }

    /// <summary>Starts the span of one scope's delta processing.</summary>
    public static Activity? StartDeltaScope(SyncJobId syncJobId, RemoteItemId remoteRootId)
    {
        var activity = SyncEngineTelemetry.Source.StartActivity("cloudsync.delta.process");
        activity?.SetTag("cloudsync.scope", remoteRootId.Value);

        return Tagged(activity, syncJobId);
    }

    /// <summary>Starts the span of one delta page fetched and stored.</summary>
    public static Activity? StartDeltaPage(SyncJobId syncJobId) => Tagged(SyncEngineTelemetry.Source.StartActivity("cloudsync.delta.page"), syncJobId);

    /// <summary>Starts the span of one conflict resolution.</summary>
    public static Activity? StartConflictResolution(Conflict conflict, ConflictResolution choice)
    {
        var activity = SyncEngineTelemetry.Source.StartActivity("cloudsync.conflict.resolve");
        activity?.SetTag("cloudsync.conflict.id", conflict.Id.Value.ToString());
        activity?.SetTag("cloudsync.conflict.resolution", choice.ToString());

        return Tagged(activity, conflict.SyncJobId);
    }

    /// <summary>Marks the span failed: error status with the reason, and <c>error.type</c> naming the kind of failure. Does nothing for <see langword="null"/>.</summary>
    /// <param name="activity">The span to mark.</param>
    /// <param name="errorType">Low-cardinality kind of failure, such as the error case name.</param>
    /// <param name="description">Why it failed. Must not hold tokens or secrets.</param>
    public static void Fail(Activity? activity, string errorType, string description)
    {
        activity?.SetStatus(ActivityStatusCode.Error, description);
        activity?.SetTag("error.type", errorType);
    }

    private static Activity? Tagged(Activity? activity, SyncJobId syncJobId)
    {
        activity?.SetTag("cloudsync.sync_job.id", syncJobId.Value.ToString());

        return activity;
    }

    private static Activity? Messaging(Activity? activity, string operationType, Operation operation)
    {
        activity?.SetTag("messaging.system", MessagingSystem);
        activity?.SetTag("messaging.destination.name", QueueName);
        activity?.SetTag("messaging.operation.type", operationType);
        activity?.SetTag("messaging.message.id", operation.Id.Value.ToString());
        activity?.SetTag("cloudsync.operation.type", operation.Type.ToString());
        activity?.SetTag("cloudsync.item.id", operation.ItemId.Value.ToString());

        return Tagged(activity, operation.SyncJobId);
    }
}
