namespace AStarDev.CloudSync.IPC;

/// <summary>The folders under one remote folder, answering <see cref="GetRemoteFolders"/>.</summary>
/// <param name="Folders">Sorted by name, ignoring case. Holds at most 2000 entries, the first by name. Empty when the folder holds no subfolders; files are never listed.</param>
public sealed record RemoteFoldersView(IReadOnlyList<RemoteFolderView> Folders);
