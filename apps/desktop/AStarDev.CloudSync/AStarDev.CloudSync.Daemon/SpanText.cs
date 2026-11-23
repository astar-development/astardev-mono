using System.Diagnostics;
using System.Globalization;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Packs a finished span into the <see cref="TelemetryEntry.Text"/> of the telemetry store and reads it back, so spans need no table of their own.</summary>
internal static class SpanText
{
    private const char Separator = '\t';
    private const int FieldCount = 6;
    private const string ErrorStatus = "Error";

    /// <summary>Stores the trace, span and parent ids, the duration, the status and the scrubbed status description, separated by tabs.</summary>
    public static TelemetryEntry ToEntry(Activity activity)
        => new(TelemetryKind.Span, new DateTimeOffset(activity.StartTimeUtc, TimeSpan.Zero), 0, activity.OperationName, Text(activity));

    /// <summary>Reads the entries written by <see cref="ToEntry"/>, newest first as given. An entry not in the stored shape is left out.</summary>
    public static IEnumerable<SpanView> ToViews(IEnumerable<TelemetryEntry> entries)
        => entries.Select(ToView).SelectMany(Present);

    private static IEnumerable<SpanView> Present(Option<SpanView> option) => option.Match(view => (IEnumerable<SpanView>)[view], () => []);

    private static Option<SpanView> ToView(TelemetryEntry entry)
        => entry.Text.Split(Separator, FieldCount) is [var traceId, var spanId, var parentSpanId, var duration, var status, var detail] && double.TryParse(duration, CultureInfo.InvariantCulture, out var milliseconds)
            ? Option.Some(new SpanView(traceId, spanId, parentSpanId, entry.Source, entry.Timestamp, milliseconds, status == ErrorStatus, detail))
            : Option.None<SpanView>();

    private static string Text(Activity activity)
        => string.Join(Separator, activity.TraceId, activity.SpanId, activity.ParentSpanId == default ? string.Empty : activity.ParentSpanId.ToString(), activity.Duration.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture), activity.Status, Detail(activity));

    private static string Detail(Activity activity) => LogRedaction.Scrub(activity.StatusDescription ?? string.Empty).Replace(Separator, ' ');
}
