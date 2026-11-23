namespace AStarDev.CloudSync.Ports;

/// <summary>Filter for <see cref="ITelemetryStore.QueryAsync"/>. Every filter narrows the result; the defaults match everything of the kind.</summary>
/// <param name="Kind">Only entries of this kind are returned.</param>
public sealed record TelemetryQuery(TelemetryKind Kind)
{
    /// <summary>Entries stamped before this instant are left out. The default includes everything.</summary>
    public DateTimeOffset Since { get; init; } = DateTimeOffset.MinValue;

    /// <summary>Entries with a lower <see cref="TelemetryEntry.Level"/> are left out.</summary>
    public int MinimumLevel { get; init; }

    /// <summary>Entries whose text does not contain this, ignoring ASCII case, are left out. Empty matches every entry.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The most entries returned, newest first. Values below one return nothing.</summary>
    public int Limit { get; init; } = 500;
}
