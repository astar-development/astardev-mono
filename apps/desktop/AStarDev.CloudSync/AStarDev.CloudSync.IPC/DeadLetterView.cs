namespace AStarDev.CloudSync.IPC;

/// <summary>One operation that gave up after its retries. It is never run again until <see cref="RetryOperation"/>, and stays listed until then or until <see cref="IgnoreOperation"/>.</summary>
/// <param name="OperationId">Identifies the operation in <see cref="RetryOperation"/> and <see cref="IgnoreOperation"/>.</param>
/// <param name="ItemId">The daemon's identity for the file or folder the operation acts on; the operation does not carry the path.</param>
/// <param name="Type">What the operation does, as its name, for example <c>UploadFile</c>.</param>
/// <param name="Attempts">How many times the daemon tried before giving up.</param>
/// <param name="LastError">Why the last attempt failed, worded as the daemon recorded it; <see langword="null"/> when no reason was recorded.</param>
/// <param name="CreatedUtc">When the operation was queued. The daemon does not record when it gave up.</param>
public sealed record DeadLetterView(Guid OperationId, Guid ItemId, string Type, int Attempts, string? LastError, DateTimeOffset CreatedUtc);
