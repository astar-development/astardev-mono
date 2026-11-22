using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

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

    private static long ReadOpen(SyncMetrics metrics)
    {
        long open = -1;
        Listen(metrics.Meter, (instrument, value, _) =>
        {
            if (instrument.Name == "cloudsync.sync.conflicts.open") open = value;
        }, out var listener);
        using var _ = listener;
        listener.RecordObservableInstruments();

        return open;
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
    public async Task report_stored_open_conflicts_after_a_restart()
    {
        using var metrics = new SyncMetrics();
        var store = new FakeConflictStore(new Ok<int>(3));

        var refreshed = await metrics.RefreshOpenConflictsAsync(store, TestContext.Current.CancellationToken);

        refreshed.Value.ShouldBeOfType<Ok<Unit>>();
        ReadOpen(metrics).ShouldBe(3);
    }

    [Fact]
    public async Task keep_the_last_open_conflict_count_when_the_store_fails()
    {
        using var metrics = new SyncMetrics();
        await metrics.RefreshOpenConflictsAsync(new FakeConflictStore(new Ok<int>(2)), TestContext.Current.CancellationToken);

        var refreshed = await metrics.RefreshOpenConflictsAsync(new FakeConflictStore(new Fail<StoreError>(new StorageFailed("disk"))), TestContext.Current.CancellationToken);

        refreshed.Value.ShouldBeOfType<Fail<StoreError>>();
        ReadOpen(metrics).ShouldBe(2);
    }

    [Fact]
    public async Task honour_cancellation_when_refreshing_open_conflicts()
    {
        using var metrics = new SyncMetrics();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await metrics.RefreshOpenConflictsAsync(new FakeConflictStore(new Ok<int>(1)), source.Token));
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
