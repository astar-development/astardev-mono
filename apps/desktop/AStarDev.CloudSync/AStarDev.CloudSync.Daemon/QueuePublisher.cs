using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Looks at the queue every <see cref="ApplicationConstants.QueuePollInterval"/> and publishes <see cref="QueueChanged"/> and <see cref="DashboardChanged"/> when it moved, because operations start, finish and fail on scheduler workers that know nothing of IPC.
/// A failed publish is logged and dropped: a UI that misses one sees the next.
/// </summary>
internal sealed class QueuePublisher(QueueQuery queue, DashboardSnapshotQuery dashboard, TimeProvider timeProvider, ILogger<QueuePublisher> logger) : BackgroundService
{
    /// <summary>Publishes the queue and the dashboard now when either differs from what was published last.</summary>
    /// <param name="cancellationToken">Cancelling surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task PublishAsync(CancellationToken cancellationToken)
    {
        if (await queue.PublishIfChangedAsync(cancellationToken) is Fail<IpcError> queueFail) DaemonLog.PublishFailed(logger, nameof(QueueChanged), queueFail.Error.Value, ErrorInfo.CodeOf(queueFail.Error.Value));
        if (await dashboard.PublishIfChangedAsync(cancellationToken) is Fail<IpcError> dashboardFail) DaemonLog.PublishFailed(logger, nameof(DashboardChanged), dashboardFail.Error.Value, ErrorInfo.CodeOf(dashboardFail.Error.Value));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ApplicationConstants.QueuePollInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) await PublishAsync(stoppingToken);
    }
}
