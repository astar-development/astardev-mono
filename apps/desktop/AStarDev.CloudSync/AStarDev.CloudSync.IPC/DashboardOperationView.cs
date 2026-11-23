namespace AStarDev.CloudSync.IPC;

/// <summary>One operation the daemon is executing now.</summary>
/// <param name="Type">What the operation does, as its name, for example <c>UploadFile</c>.</param>
/// <param name="Path">The item's path; empty when the daemon no longer knows the item.</param>
public sealed record DashboardOperationView(string Type, string Path);
