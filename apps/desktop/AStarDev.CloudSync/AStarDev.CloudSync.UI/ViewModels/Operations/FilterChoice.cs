namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>One entry of a filter drop-down.</summary>
/// <param name="Text">Localised text shown to the user.</param>
/// <param name="Value">What the entry selects; <see langword="null"/> for the "All" entry, which matches everything.</param>
internal sealed record FilterChoice(string Text, string? Value);
