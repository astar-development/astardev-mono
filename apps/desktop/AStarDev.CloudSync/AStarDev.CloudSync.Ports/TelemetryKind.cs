namespace AStarDev.CloudSync.Ports;

/// <summary>Which signal a <see cref="TelemetryEntry"/> records.</summary>
public enum TelemetryKind
{
    /// <summary>A log message.</summary>
    Log,

    /// <summary>A finished trace span.</summary>
    Span,

    /// <summary>A metric sample.</summary>
    Metric
}
