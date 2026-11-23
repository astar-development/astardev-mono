namespace AStarDev.CloudSync.IPC;

/// <summary>One finished operation in <see cref="HistoryView"/>.</summary>
/// <param name="OperationId">The daemon's identity for the operation.</param>
/// <param name="Provider">Type of the provider account the operation ran against, for example <c>OneDrive</c>; empty when the account is no longer known.</param>
/// <param name="Type">What the operation did, as its name, for example <c>UploadFile</c>.</param>
/// <param name="Status">The outcome: <c>Completed</c>, or <c>Failed</c> for an operation that failed or gave up after its retries.</param>
/// <param name="Timestamp">When the operation was queued. The daemon does not record when it finished.</param>
/// <param name="Path">The affected path relative to the sync root; empty when the daemon no longer knows the item.</param>
/// <param name="SizeBytes">Size of the affected item in bytes; zero when unknown.</param>
public sealed record HistoryEntryView(Guid OperationId, string Provider, string Type, string Status, DateTimeOffset Timestamp, string Path, long SizeBytes);
