using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetDiagnostics"/> from the registered health checks. The start time is the moment the daemon created the query, when it began to serve.</summary>
internal sealed class DiagnosticsQuery(HealthCheckService health, TimeProvider timeProvider)
{
    private readonly DateTimeOffset started = timeProvider.GetUtcNow();

    /// <summary>Runs every registered health check and reports name and status only.</summary>
    /// <returns>The diagnostics, never a failure: a check that throws is reported as unhealthy. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<DiagnosticsView, IpcError>> HandleAsync(GetDiagnostics query, CancellationToken cancellationToken)
    {
        var report = await health.CheckHealthAsync(cancellationToken);

        return new Ok<DiagnosticsView>(new DiagnosticsView(DaemonState.Running, ApplicationConstants.DaemonVersion, started, [.. report.Entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new HealthCheckView(entry.Key, StateOf(entry.Value.Status)))]));
    }

    private static ProviderHealthState StateOf(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => ProviderHealthState.Healthy,
        HealthStatus.Degraded => ProviderHealthState.Degraded,
        _ => ProviderHealthState.Unhealthy
    };
}
