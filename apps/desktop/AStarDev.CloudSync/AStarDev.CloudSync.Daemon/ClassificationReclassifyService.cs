using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.Persistence;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Reclassifies the stored files in the background once startup has finished, when no run was ever recorded or a newer seed was applied since the last one.
/// It starts after the startup sequence, so a long run never delays the daemon accepting connections, and it is skipped on a database in recovery mode.
/// </summary>
/// <param name="reclassifier">Runs the reclassify.</param>
/// <param name="tracker">Runs it one at a time and publishes its progress.</param>
/// <param name="databaseState">Tells whether the database can be trusted.</param>
internal sealed class ClassificationReclassifyService(IClassificationReclassifier reclassifier, ReclassifyTracker tracker, IDatabaseState databaseState) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (databaseState.Mode != DatabaseMode.Normal) return;

        await Task.Yield();
        _ = await tracker.RunAsync(progress => reclassifier.StartIfStaleAsync(progress, stoppingToken), stoppingToken);
    }
}
