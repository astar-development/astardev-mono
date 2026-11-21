using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Reports the database unhealthy until startup has verified it, whenever recovery mode is active, and when the file cannot be opened.</summary>
internal sealed class DatabaseHealthCheck(IDatabaseState state, IDbContextFactory<CloudSyncDbContext> contextFactory) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (state.Mode)
        {
            case DatabaseMode.Uninitialised:
                return HealthCheckResult.Unhealthy("The database has not been initialised yet.");
            case DatabaseMode.Recovery:
                return HealthCheckResult.Unhealthy($"The database failed its integrity check and the daemon is in recovery mode: {state.RecoveryReason}");
            default:
                return await CheckConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<HealthCheckResult> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await database.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database file cannot be opened.");
    }
}
