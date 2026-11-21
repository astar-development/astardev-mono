namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Everything the dashboard shows at one moment, as raw values; <c>DashboardViewModel</c> localises and formats them.</summary>
/// <param name="Health">Overall health for the Trust Status card.</param>
/// <param name="LastReconciliation">When the last reconciliation succeeded.</param>
/// <param name="LastProviderPoll">When a provider was last polled.</param>
/// <param name="LastQueueActivity">When the queue last did anything.</param>
/// <param name="ItemsAnalysed">Items analysed so far.</param>
/// <param name="ItemsTotal">Items to analyse in total; never below <paramref name="ItemsAnalysed"/> for a sensible display.</param>
/// <param name="Stage">The pipeline stage running now; earlier stages show as done, later ones as pending.</param>
/// <param name="ItemsPerSecond">Current throughput.</param>
/// <param name="EstimatedRemaining">Estimated time left, shown in whole minutes.</param>
/// <param name="Providers">Each connected provider with its health.</param>
/// <param name="Pending">Queued operations not yet started.</param>
/// <param name="Running">Operations executing now.</param>
/// <param name="Completed">Operations finished successfully.</param>
/// <param name="Failed">Operations that failed.</param>
/// <param name="Conflicts">Unresolved conflicts. Zero is still shown.</param>
/// <param name="Errors">Errors needing attention. Zero is still shown.</param>
/// <param name="Warnings">Warnings. Zero is still shown.</param>
/// <param name="Activities">Operations running now, each with its path.</param>
/// <param name="Events">Recent events, newest first.</param>
internal sealed record DashboardSnapshot(
    SyncHealth Health,
    DateTimeOffset LastReconciliation,
    DateTimeOffset LastProviderPoll,
    DateTimeOffset LastQueueActivity,
    long ItemsAnalysed,
    long ItemsTotal,
    ReconciliationStage Stage,
    long ItemsPerSecond,
    TimeSpan EstimatedRemaining,
    IReadOnlyList<(string Name, SyncHealth Health)> Providers,
    long Pending,
    long Running,
    long Completed,
    long Failed,
    int Conflicts,
    int Errors,
    int Warnings,
    IReadOnlyList<(DashboardOperation Operation, string Path)> Activities,
    IReadOnlyList<DashboardEvent> Events);
