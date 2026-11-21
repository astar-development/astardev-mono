namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One pipeline stage as the dashboard lists it.</summary>
/// <param name="Name">Localised stage name.</param>
/// <param name="State">Localised progress word: done, running or pending.</param>
/// <param name="IsCurrent">Whether this is the stage running now.</param>
internal sealed record PipelineStageRow(string Name, string State, bool IsCurrent);
