using System.Threading.Channels;
using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Runs reclassify runs for the daemon, one at a time, and tells every subscribed UI how far the active one has got with <see cref="ReclassifyProgressChanged"/>.
/// Progress reports from the run are coalesced: only the latest one waits for the publisher, so a slow client never slows the run.
/// A failed publish is logged and dropped: a UI that misses one report sees the next.
/// </summary>
/// <param name="server">Publishes the events.</param>
/// <param name="logger">Reports a failed publish.</param>
internal sealed class ReclassifyTracker(IIpcServer server, ILogger<ReclassifyTracker> logger)
{
    private readonly Lock gate = new();
    private bool running;
    private long filesDone;

    /// <summary>Whether a run is active.</summary>
    public bool Running
    {
        get
        {
            lock (gate) return running;
        }
    }

    /// <summary>Files gone through in the active run, or in the last one when none is active.</summary>
    public long FilesDone
    {
        get
        {
            lock (gate) return filesDone;
        }
    }

    /// <summary>Runs <paramref name="run"/>, publishing its progress, and publishes a last event when it ends. When a run is already active the call returns at once and <paramref name="run"/> is not called.</summary>
    /// <param name="run">Starts the run with the progress sink to report to.</param>
    /// <param name="cancellationToken">Cancelling stops the publishing; the run itself is cancelled through the token it was given.</param>
    /// <returns>What <paramref name="run"/> returned, or <see cref="ReclassifyAlreadyRunning"/>.</returns>
    public async Task<ReclassifyOutcome> RunAsync(Func<IProgress<ReclassifyProgress>, Task<ReclassifyOutcome>> run, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (running) return new ReclassifyAlreadyRunning();

            running = true;
            filesDone = 0;
        }

        var reports = Channel.CreateBounded<ReclassifyProgress>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        var publishing = PublishAsync(reports.Reader, cancellationToken);
        try
        {
            return await run(new ChannelProgress(reports.Writer, this)).ConfigureAwait(false);
        }
        finally
        {
            reports.Writer.Complete();
            await publishing.ConfigureAwait(false);
            lock (gate) running = false;
            await PublishOneAsync(false, FilesDone, cancellationToken).ConfigureAwait(false);
        }
    }

    private void Record(long done)
    {
        lock (gate) filesDone = done;
    }

    private async Task PublishAsync(ChannelReader<ReclassifyProgress> reports, CancellationToken cancellationToken)
    {
        await foreach (var progress in reports.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            await PublishOneAsync(true, progress.FilesDone, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishOneAsync(bool isRunning, long done, CancellationToken cancellationToken)
    {
        if (await server.PublishAsync(new ReclassifyProgressChanged(isRunning, done), cancellationToken).ConfigureAwait(false) is Fail<IpcError> fail) DaemonLog.PublishFailed(logger, nameof(ReclassifyProgressChanged), fail.Error.Value, ErrorInfo.CodeOf(fail.Error.Value));
    }

    private sealed class ChannelProgress(ChannelWriter<ReclassifyProgress> writer, ReclassifyTracker tracker) : IProgress<ReclassifyProgress>
    {
        public void Report(ReclassifyProgress value)
        {
            tracker.Record(value.FilesDone);
            writer.TryWrite(value);
        }
    }
}
