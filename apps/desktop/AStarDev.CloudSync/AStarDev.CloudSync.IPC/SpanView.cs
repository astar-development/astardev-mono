namespace AStarDev.CloudSync.IPC;

/// <summary>One stored span, ready to show as it is.</summary>
/// <param name="TraceId">The trace the span belongs to. A UI request and the daemon span it caused share it.</param>
/// <param name="SpanId">The span's own identifier.</param>
/// <param name="ParentSpanId">The span this one ran inside, possibly in the UI process and so absent from the result. Empty for the first span of a trace.</param>
/// <param name="Name">What the span measured, such as <c>persistence.save</c>.</param>
/// <param name="StartUtc">When the span started.</param>
/// <param name="DurationMilliseconds">How long the span ran.</param>
/// <param name="Failed">True when the span ended with an error status.</param>
/// <param name="Detail">The error description of a failed span, with any path or e-mail address already replaced; empty otherwise.</param>
public sealed record SpanView(string TraceId, string SpanId, string ParentSpanId, string Name, DateTimeOffset StartUtc, double DurationMilliseconds, bool Failed, string Detail);
