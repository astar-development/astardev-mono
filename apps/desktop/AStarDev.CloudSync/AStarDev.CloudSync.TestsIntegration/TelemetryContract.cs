namespace AStarDev.CloudSync.TestsIntegration;

internal static class TelemetryContract
{
    public const string SourcePrefix = "AStarDev.CloudSync.";

    public static IReadOnlyList<string> Metrics { get; } =
    [
        "cloudsync.queue.pending",
        "cloudsync.queue.running",
        "cloudsync.queue.dead_lettered",
        "cloudsync.queue.operation.duration",
        "cloudsync.queue.operation.latency",
        "cloudsync.queue.operation.completed",
        "cloudsync.queue.operation.failed",
        "cloudsync.sync.jobs.active",
        "cloudsync.sync.items_synchronised",
        "cloudsync.sync.conflicts.open",
        "cloudsync.sync.conflict.resolutions",
        "cloudsync.syncengine.items_indexed",
        "cloudsync.syncengine.crawl.duration",
        "cloudsync.performance.reconciliation.duration",
        "cloudsync.performance.delta.duration",
        "cloudsync.provider.requests",
        "onedrive.requests",
        "onedrive.failures",
        "onedrive.rate_limits",
        "cloudsync.persistence.saves",
        "cloudsync.persistence.save.duration",
        "cloudsync.ipc.requests",
        "cloudsync.ipc.request.duration",
        "cloudsync.ipc.connections"
    ];

    public static IReadOnlyList<string> Spans { get; } =
    [
        "crawl.scope",
        "crawl.page",
        "cloudsync.plan",
        "cloudsync.item.plan",
        "cloudsync.reconcile",
        "cloudsync.operation.plan",
        "operations publish",
        "operations process",
        "cloudsync.provider.request",
        "cloudsync.snapshot.commit",
        "cloudsync.delta.process",
        "cloudsync.delta.page",
        "cloudsync.conflict.resolve",
        "persistence.save",
        "OneDrive.GetChildrenPage"
    ];

    public static IReadOnlyList<int> StateTransitionLogEvents { get; } = [1100, 1101, 1102, 1103, 1104, 1105, 1002, 1005];
}
