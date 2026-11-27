namespace AStarDev.CloudSync.IPC;

/// <summary>What the status bar shows, answering <see cref="GetSyncStatus"/> and carried by <see cref="SyncStatusChanged"/>.</summary>
/// <param name="ProviderHealth">The provider health check's verdict.</param>
/// <param name="QueueDepth">Operations waiting or running: the pending, running and failed ones in the operation store. Dead-lettered ones are counted in <paramref name="ErrorCount"/> instead.</param>
/// <param name="ErrorCount">Operations that gave up after their retries plus sync jobs that stopped on an error, all waiting for a decision.</param>
/// <param name="LastActivityAt">When the latest sync progress was reported since the daemon started. Null before any progress.</param>
public sealed record SyncStatusView(ProviderHealthState ProviderHealth, int QueueDepth, int ErrorCount, DateTimeOffset? LastActivityAt);
