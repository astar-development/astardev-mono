using System.Threading.Channels;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Persistence;

/// <summary>
/// Queues telemetry in a bounded channel and stores it in batches. A full queue or a failed batch drops the entries and counts them in <see cref="DroppedCount"/>;
/// the caller of <see cref="TryWrite"/> is never delayed.
/// </summary>
internal sealed class TelemetryBatchWriter(ITelemetryStore store, TimeProvider timeProvider, ILogger<TelemetryBatchWriter> logger) : BackgroundService, ITelemetryWriter
{
    private const int QueueCapacity = 10_000;
    private const int BatchSize = 500;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(250);

    private readonly Channel<TelemetryEntry> queue = Channel.CreateBounded<TelemetryEntry>(new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    private long dropped;

    /// <inheritdoc/>
    public long DroppedCount => Interlocked.Read(ref dropped);

    /// <inheritdoc/>
    public bool TryWrite(TelemetryEntry entry)
    {
        if (queue.Writer.TryWrite(entry)) return true;

        Interlocked.Increment(ref dropped);

        return false;
    }

    /// <summary>Stores everything queued so far as one or more batches. Used by the background loop and when the host stops.</summary>
    /// <param name="cancellationToken">Cancelling stops between batches.</param>
    /// <returns>A task that completes when the queue is empty.</returns>
    internal async Task FlushAsync(CancellationToken cancellationToken)
    {
        var batch = new List<TelemetryEntry>(BatchSize);
        while (batch.Count < BatchSize && queue.Reader.TryRead(out var entry)) batch.Add(entry);

        if (batch.Count == 0) return;

        var result = await store.AppendAsync(batch, cancellationToken).ConfigureAwait(false);
        if (result.Value is Fail<StoreError> failure) Drop(batch.Count, failure.Error.Value.ToString() ?? string.Empty, ErrorInfo.CodeOf(failure.Error.Value));

        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            await Task.Delay(FlushInterval, timeProvider, stoppingToken).ConfigureAwait(false);
            await FlushAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private void Drop(int count, string reason, ErrorCode errorCode)
    {
        Interlocked.Add(ref dropped, count);
        PersistenceLog.TelemetryBatchDropped(logger, count, reason, errorCode);
    }
}
