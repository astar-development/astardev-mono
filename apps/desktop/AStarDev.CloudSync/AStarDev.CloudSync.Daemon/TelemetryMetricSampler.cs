using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Reads every instrument of every <c>AStarDev.CloudSync.*</c> meter once per <see cref="ApplicationConstants.MetricSampleInterval"/> and hands one sample per instrument to the <see cref="ITelemetryWriter"/>, which batches them into the telemetry store,
/// so the Metrics screen can show their history after a restart. Tags are folded together, so a sample never carries a path, an e-mail address or any other tag value. Writing never blocks and never fails the caller.
/// Sampling only reads instruments and queues entries; nothing it calls records a measurement, and meters of other names, such as Entity Framework's, are not sampled, so storing samples cannot produce more samples.
/// </summary>
internal sealed class TelemetryMetricSampler(ITelemetryWriter writer, TimeProvider timeProvider) : BackgroundService
{
    private readonly ConcurrentDictionary<string, Series> series = new();
    private MeterListener? listener;

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        listener = new MeterListener { InstrumentPublished = Subscribe };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => Record(instrument, value));
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => Record(instrument, value));
        listener.Start();

        return base.StartAsync(cancellationToken);
    }

    /// <summary>Reads every instrument now and queues one sample for each that has been measured since the last call or, for a gauge or counter, ever. Does nothing before the sampler starts.</summary>
    public void Sample()
    {
        if (listener is null) return;

        listener.RecordObservableInstruments();
        var now = timeProvider.GetUtcNow();
        foreach (var (name, instrumentSeries) in series)
            if (instrumentSeries.Take() is { } value) writer.TryWrite(MetricText.ToEntry(name, instrumentSeries.Unit, value, now));
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        listener?.Dispose();
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ApplicationConstants.MetricSampleInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) Sample();
    }

    private void Subscribe(Instrument instrument, MeterListener source)
    {
        if (!instrument.Meter.Name.StartsWith(ApplicationConstants.ActivitySourcePrefix, StringComparison.Ordinal)) return;

        series.TryAdd(instrument.Name, new Series(instrument));
        source.EnableMeasurementEvents(instrument);
    }

    private void Record(Instrument instrument, double value)
    {
        if (series.TryGetValue(instrument.Name, out var instrumentSeries)) instrumentSeries.Add(value);
    }

    private sealed class Series(Instrument instrument)
    {
        private readonly Lock gate = new();
        private readonly bool averages = IsHistogram(instrument);
        private readonly bool accumulates = !instrument.IsObservable && !IsHistogram(instrument);
        private double total;
        private int count;

        public string Unit => instrument.Unit ?? string.Empty;

        public void Add(double value)
        {
            lock (gate)
            {
                total = accumulates || averages ? total + value : value;
                count++;
            }
        }

        public double? Take()
        {
            lock (gate)
            {
                if (count == 0) return null;

                var value = averages ? total / count : total;
                if (averages) (total, count) = (0, 0);

                return value;
            }
        }

        private static bool IsHistogram(Instrument instrument) => instrument.GetType() is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(Histogram<>);
    }
}
