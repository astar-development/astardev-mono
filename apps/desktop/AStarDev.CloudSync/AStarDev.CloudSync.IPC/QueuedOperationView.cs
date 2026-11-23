namespace AStarDev.CloudSync.IPC;

/// <summary>One operation that is waiting to start or executing now.</summary>
/// <param name="OperationId">Identifies the operation.</param>
/// <param name="Provider">Provider type of the operation's account, for example <c>OneDrive</c>; empty when the daemon no longer knows the account.</param>
/// <param name="Type">What the operation does, as its name, for example <c>UploadFile</c>.</param>
/// <param name="Status">Where the operation is: <c>Pending</c> or <c>Running</c>.</param>
/// <param name="Path">The item's path; empty when the daemon no longer knows the item.</param>
/// <param name="SizeBytes">Size of the item in bytes; zero when unknown.</param>
/// <param name="QueuedUtc">When the operation was queued.</param>
public sealed record QueuedOperationView(Guid OperationId, string Provider, string Type, string Status, string Path, long SizeBytes, DateTimeOffset QueuedUtc);
