using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Everything the dashboard shows at one moment, as raw values; <c>DashboardViewModel</c> localises and formats them.</summary>
/// <param name="Health">Overall health for the Trust Status card.</param>
/// <param name="LastReconciliation">When the last reconciliation succeeded; none before the first.</param>
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
    Option<DateTimeOffset> LastReconciliation,
    Option<DateTimeOffset> LastProviderPoll,
    Option<DateTimeOffset> LastQueueActivity,
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
    IReadOnlyList<DashboardEvent> Events)
{
    /// <summary>What the dashboard shows while the daemon link is down: nothing known, so critical health, zero counts and empty lists.</summary>
    public static DashboardSnapshot Disconnected { get; } = new(SyncHealth.Critical, Option.None<DateTimeOffset>(), Option.None<DateTimeOffset>(), Option.None<DateTimeOffset>(), 0, 0, ReconciliationStage.EnumerateLocalState, 0, TimeSpan.Zero, [], 0, 0, 0, 0, 0, 0, 0, [], []);

    /// <summary>Shows what the daemon reported, as reported.</summary>
    /// <param name="view">The daemon's dashboard. A throughput or estimate it does not know shows as zero; an event or operation name this version does not know is left out.</param>
    /// <returns>The snapshot. Health is critical when the provider is unhealthy, attention required when a conflict or an error waits, degraded when the provider needs the user, active while work is queued, running or unfinished, otherwise healthy.</returns>
    public static DashboardSnapshot From(DashboardView view)
        => new(
            HealthOf(view),
            Option.FromNullable(view.LastReconciliation),
            Option.FromNullable(view.LastProviderPoll),
            Option.FromNullable(view.LastQueueActivity),
            view.ItemsAnalysed,
            view.ItemsTotal ?? 0,
            StageOf(view.Stage),
            (long)Math.Round(view.ItemsPerSecond ?? 0),
            view.EstimatedRemaining ?? TimeSpan.Zero,
            [.. view.Providers.Select(provider => (provider.Name, ProviderHealthOf(provider.Health)))],
            view.Pending,
            view.Running,
            view.Completed,
            view.Failed,
            view.Conflicts,
            view.Errors,
            view.Warnings,
            [.. view.Activities.Select(activity => (OperationOf(activity.Type), activity.Path))],
            [.. view.Events.Where(name => Enum.IsDefined(typeof(DashboardEvent), name)).Select(Enum.Parse<DashboardEvent>)]);

    private static SyncHealth HealthOf(DashboardView view)
    {
        if (view.ProviderHealth == ProviderHealthState.Unhealthy) return SyncHealth.Critical;
        if (view.Conflicts > 0 || view.Errors > 0) return SyncHealth.AttentionRequired;
        if (view.ProviderHealth == ProviderHealthState.Degraded) return SyncHealth.Degraded;

        return view.Pending > 0 || view.Running > 0 || view.ItemsAnalysed < view.ItemsTotal ? SyncHealth.Active : SyncHealth.Healthy;
    }

    private static SyncHealth ProviderHealthOf(ProviderHealthState health) => health switch
    {
        ProviderHealthState.Healthy => SyncHealth.Healthy,
        ProviderHealthState.Degraded => SyncHealth.Degraded,
        _ => SyncHealth.Critical
    };

    private static ReconciliationStage StageOf(DashboardStage stage) => stage switch
    {
        DashboardStage.EnumerateLocalState => ReconciliationStage.EnumerateLocalState,
        DashboardStage.EnumerateRemoteState => ReconciliationStage.EnumerateRemoteState,
        DashboardStage.BuildKnownState => ReconciliationStage.BuildKnownState,
        DashboardStage.AnalyseChanges => ReconciliationStage.AnalyseChanges,
        DashboardStage.GenerateOperations => ReconciliationStage.GenerateOperations,
        _ => ReconciliationStage.ExecuteQueue
    };

    private static DashboardOperation OperationOf(string type) => type switch
    {
        "UploadFile" => DashboardOperation.Upload,
        "DownloadFile" => DashboardOperation.Download,
        "RenameRemote" or "RenameLocal" => DashboardOperation.Rename,
        _ => DashboardOperation.Delete
    };
}
