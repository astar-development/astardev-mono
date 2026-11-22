using System.Diagnostics.Metrics;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class PerformanceMetricsShould
{
    [Fact]
    public void record_reconciliation_and_delta_duration()
    {
        using var metrics = new PerformanceMetrics();
        var seen = new Dictionary<string, double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == metrics.Meter) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => seen[instrument.Name] = value);
        listener.Start();

        metrics.RecordReconciliation(TimeSpan.FromMilliseconds(1500));
        metrics.RecordDelta(TimeSpan.FromSeconds(3));

        seen.ShouldBe(new Dictionary<string, double> { ["cloudsync.performance.reconciliation.duration"] = 1.5, ["cloudsync.performance.delta.duration"] = 3 });
    }
}
