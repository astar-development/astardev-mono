using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class RecordedMetrics : IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<KeyValuePair<string, object?>[]>> measurements = new();
    private readonly ConcurrentDictionary<string, byte> published = new();
    private readonly MeterListener listener = new();

    public RecordedMetrics()
    {
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (!instrument.Meter.Name.StartsWith(TelemetryContract.SourcePrefix, StringComparison.Ordinal)) return;

            published[instrument.Name] = 0;
            meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Add(instrument.Name, tags));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Add(instrument.Name, tags));
        listener.Start();
    }

    public IReadOnlyCollection<string> Published => [.. published.Keys];

    public IReadOnlyCollection<string> Measured => [.. measurements.Keys];

    public IReadOnlyList<KeyValuePair<string, object?>[]> TagsOf(string instrument)
        => measurements.TryGetValue(instrument, out var tags) ? [.. tags] : [];

    public void Observe() => listener.RecordObservableInstruments();

    public void Dispose() => listener.Dispose();

    private void Add(string name, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => measurements.GetOrAdd(name, _ => []).Add(tags.ToArray());
}
