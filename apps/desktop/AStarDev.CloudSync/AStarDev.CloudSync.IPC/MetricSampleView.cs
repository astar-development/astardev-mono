namespace AStarDev.CloudSync.IPC;

/// <summary>One stored sample of one instrument, ready to show as it is.</summary>
/// <param name="Name">The instrument name, such as <c>cloudsync.queue.pending</c>.</param>
/// <param name="TimestampUtc">When the sample was taken.</param>
/// <param name="Value">A gauge's reading, a counter's running total since the daemon started, or the mean of a histogram's recordings since the previous sample. Tags are not kept.</param>
/// <param name="Unit">The instrument's unit, such as <c>{operation}</c> or <c>s</c>; empty when it has none.</param>
public sealed record MetricSampleView(string Name, DateTimeOffset TimestampUtc, double Value, string Unit);
