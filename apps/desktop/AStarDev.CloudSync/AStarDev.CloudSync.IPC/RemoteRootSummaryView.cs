namespace AStarDev.CloudSync.IPC;

/// <summary>What the drive root held when the daemon last listed it.</summary>
/// <param name="ItemCount">Files and folders directly under the root. Items inside subfolders are not counted.</param>
public sealed record RemoteRootSummaryView(int ItemCount);
