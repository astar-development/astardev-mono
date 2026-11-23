namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One stored span as text, ready to show as it is.</summary>
/// <param name="Header">Date, time, name and duration on one line, saying so when the span failed.</param>
/// <param name="Ids">The trace id, span id and parent span id on one line.</param>
/// <param name="Detail">The error description of a failed span; empty otherwise.</param>
internal sealed record SpanRowViewModel(string Header, string Ids, string Detail);
