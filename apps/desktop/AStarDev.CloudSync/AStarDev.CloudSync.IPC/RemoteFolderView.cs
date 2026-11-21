namespace AStarDev.CloudSync.IPC;

/// <summary>One remote folder as the UI shows it in the tree.</summary>
/// <param name="Id">The provider's id for the folder. Pass it to <see cref="GetRemoteFolders"/> to list what is inside.</param>
/// <param name="Name">The folder's own name, without the path above it.</param>
public sealed record RemoteFolderView(string Id, string Name);
