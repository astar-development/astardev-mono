namespace AStarDev.CloudSync.Persistence;

/// <summary>A row of the <c>TelemetryEntries</c> table: one log, span or metric sample.</summary>
internal sealed class TelemetryEntryEntity
{
    /// <summary>Row identity, assigned by SQLite.</summary>
    public long Id { get; set; }

    /// <summary>The <c>TelemetryKind</c> as a number.</summary>
    public int Kind { get; set; }

    /// <summary>UTC ticks, so SQLite can compare and order them.</summary>
    public long TimestampTicks { get; set; }

    /// <summary>Severity for logs, zero otherwise.</summary>
    public int Level { get; set; }

    /// <summary>Logger category, span name or metric name.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Message or details.</summary>
    public string Text { get; set; } = string.Empty;
}
