using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class SyncMetricsShould
{
    private static void Listen(Meter meter, Action<Instrument, long, IReadOnlyList<KeyValuePair<string, object?>>> onLong, out MeterListener listener)
    {
        listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == meter) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => onLong(instrument, value, tags.ToArray()));
        listener.Start();
    }

    [Fact]
    public void count_items_synchronised()
    {
        using var metrics = new SyncMetrics();
        var counts = new Dictionary<string, long>();
        Listen(metrics.Meter, (instrument, value, tags) =>
        {
            if (instrument.Name == "cloudsync.sync.items_synchronised") counts[(string)tags.Single().Value!] = counts.GetValueOrDefault((string)tags.Single().Value!) + value;
        }, out var listener);
        using var _ = listener;

        metrics.ItemsSynchronised(OperationType.UploadFile);
        metrics.ItemsSynchronised(OperationType.UploadFile);
        metrics.ItemsSynchronised(OperationType.DownloadFile);

        counts.ShouldBe(new Dictionary<string, long> { ["UploadFile"] = 2, ["DownloadFile"] = 1 });
    }

    [Fact]
    public void report_open_conflicts()
    {
        using var metrics = new SyncMetrics();
        long open = -1;
        Listen(metrics.Meter, (instrument, value, _) =>
        {
            if (instrument.Name == "cloudsync.sync.conflicts.open") open = value;
        }, out var listener);
        using var _ = listener;

        metrics.ConflictOpened();
        metrics.ConflictOpened();
        metrics.ConflictResolved("KeepLocal");
        listener.RecordObservableInstruments();

        open.ShouldBe(1);
    }

    [Fact]
    public void never_report_fewer_than_zero_open_conflicts()
    {
        using var metrics = new SyncMetrics();
        long open = -1;
        Listen(metrics.Meter, (instrument, value, _) =>
        {
            if (instrument.Name == "cloudsync.sync.conflicts.open") open = value;
        }, out var listener);
        using var _ = listener;

        metrics.ConflictResolved("KeepRemote");
        listener.RecordObservableInstruments();

        open.ShouldBe(0);
    }

    [Fact]
    public void count_conflict_resolutions()
    {
        using var metrics = new SyncMetrics();
        var counts = new Dictionary<string, long>();
        Listen(metrics.Meter, (instrument, value, tags) =>
        {
            if (instrument.Name == "cloudsync.sync.conflict.resolutions") counts[(string)tags.Single().Value!] = counts.GetValueOrDefault((string)tags.Single().Value!) + value;
        }, out var listener);
        using var _ = listener;

        metrics.ConflictResolved("KeepLocal");
        metrics.ConflictResolved("KeepBoth");
        metrics.ConflictResolved("KeepLocal");

        counts.ShouldBe(new Dictionary<string, long> { ["KeepLocal"] = 2, ["KeepBoth"] = 1 });
    }

    [Fact]
    public void track_active_jobs()
    {
        using var metrics = new SyncMetrics();
        long active = 0;
        Listen(metrics.Meter, (instrument, value, _) =>
        {
            if (instrument.Name == "cloudsync.sync.jobs.active") active += value;
        }, out var listener);
        using var _ = listener;

        metrics.JobStarted();
        metrics.JobStarted();
        metrics.JobStopped();

        active.ShouldBe(1);
    }
}
