namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>A tick or untick of one remote folder.</summary>
/// <param name="FolderId">The provider's id of the folder.</param>
/// <param name="Selected"><see langword="true"/> when the folder was ticked.</param>
public sealed record ScopeChange(string FolderId, bool Selected);
