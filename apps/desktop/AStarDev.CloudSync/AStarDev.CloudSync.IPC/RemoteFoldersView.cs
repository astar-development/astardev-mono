namespace AStarDev.CloudSync.IPC;

/// <summary>The folders under one remote folder, answering <see cref="GetRemoteFolders"/>.</summary>
/// <param name="Total">How many subfolders the folder holds in all, so a caller can ask for the next page.</param>
/// <param name="Folders">This page of the subfolders, sorted by name ignoring case. Empty when the folder holds no subfolders or the page is past the end; files are never listed.</param>
public sealed record RemoteFoldersView(int Total, IReadOnlyList<RemoteFolderView> Folders);
