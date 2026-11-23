namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetMetrics"/>.</summary>
/// <param name="Samples">The matching samples, newest first, of every instrument; empty when nothing matches. The first sample of a name is its current value.</param>
public sealed record MetricsView(IReadOnlyList<MetricSampleView> Samples);
