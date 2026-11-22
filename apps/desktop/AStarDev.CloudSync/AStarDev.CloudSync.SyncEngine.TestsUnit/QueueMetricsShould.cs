using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class QueueMetricsShould
{
    private static readonly DateTimeOffset Created = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static Operation Queued(int attempts) => new(new OperationId(Guid.NewGuid()), new SyncJobId(Guid.NewGuid()), new ItemId(Guid.NewGuid()), OperationType.UploadFile, OperationPriority.Background, OperationStatus.Running, attempts, Created, null, Created);

    [Fact]
    public void record_queue_latency_and_completed_and_failed()
    {
        using var metrics = new QueueMetrics();
        var latencies = new List<double>();
        var completed = 0L;
        var failed = new List<(long Value, string Type, string Error)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
        {
            if (instrument.Name == "cloudsync.queue.operation.latency") latencies.Add(value);
        });
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "cloudsync.queue.operation.completed") completed += value;
            if (instrument.Name == "cloudsync.queue.operation.failed") failed.Add((value, (string)tags[0].Value!, (string)tags[1].Value!));
        });
        listener.Start();

        metrics.RecordStarted(Queued(1), Created.AddSeconds(5));
        metrics.RecordStarted(Queued(2), Created.AddSeconds(500));
        metrics.RecordCompleted(OperationType.UploadFile);
        metrics.RecordFailed(OperationType.DownloadFile, "Transient");

        latencies.ShouldBe([5d]);
        completed.ShouldBe(1);
        failed.ShouldBe([(1L, "DownloadFile", "Transient")]);
    }
}
