namespace AStarDev.CloudSync.IPC;

/// <summary>One classification category.</summary>
/// <param name="Id">The category's identity, which stays when it is renamed.</param>
/// <param name="Name">The name shown to the user.</param>
public sealed record ClassificationCategoryView(Guid Id, string Name);
