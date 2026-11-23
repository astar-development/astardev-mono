using System.Diagnostics;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Hands every span that finishes in a <c>AStarDev.CloudSync.*</c> activity source to the <see cref="ITelemetryWriter"/>, which batches them into the telemetry store, so the Traces screen can show them after a restart.
/// Writing never blocks and never fails the caller: a full queue or a failing store drops the span. Spans of other sources, such as Entity Framework's, are not stored, and storing a batch starts no
/// <c>AStarDev.CloudSync</c> span, so storing spans cannot produce more spans.
/// </summary>
internal sealed class TelemetrySpanListener(ITelemetryWriter writer) : IHostedService, IDisposable
{
    private ActivityListener? listener;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(ApplicationConstants.ActivitySourcePrefix, StringComparison.Ordinal),
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => writer.TryWrite(SpanText.ToEntry(activity))
        };
        ActivitySource.AddActivityListener(listener);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose() => listener?.Dispose();
}
