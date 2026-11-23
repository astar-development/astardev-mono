namespace AStarDev.CloudSync.Ports;

/// <summary>One stored log message, span or metric sample.</summary>
/// <param name="Kind">The signal this entry records.</param>
/// <param name="Timestamp">When it happened. Stored with tick precision in UTC; the offset is not kept.</param>
/// <param name="Level">Severity as a <c>LogLevel</c> number for logs; zero for spans and metrics.</param>
/// <param name="Source">The logger category, span name or metric name.</param>
/// <param name="Text">The rendered message, or the formatted span or sample details.</param>
public sealed record TelemetryEntry(TelemetryKind Kind, DateTimeOffset Timestamp, int Level, string Source, string Text);
