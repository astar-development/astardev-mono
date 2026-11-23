namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetTraces"/>.</summary>
/// <param name="Spans">The matching spans, newest first, from every trace they belong to; empty when nothing matches. Group them by <see cref="SpanView.TraceId"/> and link them by <see cref="SpanView.ParentSpanId"/> to rebuild a trace.</param>
public sealed record TracesView(IReadOnlyList<SpanView> Spans);
