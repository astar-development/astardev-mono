using System.Globalization;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Packs a metric sample into the <see cref="TelemetryEntry.Text"/> of the telemetry store and reads it back, so samples need no table of their own. The instrument name is the entry's source; tags are never stored.</summary>
internal static class MetricText
{
    private const char Separator = '\t';
    private const int FieldCount = 2;

    /// <summary>Stores the value and the unit, separated by a tab.</summary>
    public static TelemetryEntry ToEntry(string name, string unit, double value, DateTimeOffset timestamp)
        => new(TelemetryKind.Metric, timestamp, 0, name, $"{value.ToString("R", CultureInfo.InvariantCulture)}{Separator}{unit.Replace(Separator, ' ')}");

    /// <summary>Reads the entries written by <see cref="ToEntry"/>, in the order given. An entry not in the stored shape is left out.</summary>
    public static IEnumerable<MetricSampleView> ToViews(IEnumerable<TelemetryEntry> entries)
        => entries.Select(ToView).SelectMany(Present);

    private static IEnumerable<MetricSampleView> Present(Option<MetricSampleView> option) => option.Match(view => (IEnumerable<MetricSampleView>)[view], () => []);

    private static Option<MetricSampleView> ToView(TelemetryEntry entry)
        => entry.Text.Split(Separator, FieldCount) is [var value, var unit] && double.TryParse(value, CultureInfo.InvariantCulture, out var number)
            ? Option.Some(new MetricSampleView(entry.Source, entry.Timestamp, number, unit))
            : Option.None<MetricSampleView>();
}
