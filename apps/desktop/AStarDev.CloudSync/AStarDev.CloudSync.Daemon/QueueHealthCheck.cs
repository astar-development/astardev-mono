using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Reports whether the operation queue is making progress: degraded while any operation is dead-lettered, unhealthy when the queue cannot be read.</summary>
internal sealed class QueueHealthCheck(IOperationStore operations) : IHealthCheck
{
    /// <inheritdoc/>
    /// <remarks>An empty queue is healthy. The description of a degraded result gives the number of dead-lettered operations.</remarks>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => await operations.CountByStatusAsync(cancellationToken) switch
        {
            Ok<IReadOnlyDictionary<OperationStatus, int>> counts => Describe(counts.Value.GetValueOrDefault(OperationStatus.DeadLetter)),
            Fail<StoreError> fail => HealthCheckResult.Unhealthy(StoreErrorMessages.Describe(fail.Error, "read", "operation queue"))
        };

    private static HealthCheckResult Describe(int deadLettered)
        => deadLettered == 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Degraded($"{deadLettered} operations were given up on after repeated failures.");
}
