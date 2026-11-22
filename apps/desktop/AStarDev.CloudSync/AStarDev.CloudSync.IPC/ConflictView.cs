namespace AStarDev.CloudSync.IPC;

/// <summary>One conflict waiting for the user, with what the Conflict Centre needs to explain it: the item's path and what each side holds.</summary>
/// <param name="ConflictId">Identifies the conflict, for the later resolve command.</param>
/// <param name="SyncJobId">The job the conflict belongs to.</param>
/// <param name="ItemId">The daemon's identity for the file in dispute.</param>
/// <param name="Type">The competing intents as the name of the conflict type, for example <c>ModifyModify</c>.</param>
/// <param name="DetectedUtc">When the daemon detected the conflict.</param>
/// <param name="Status">Whether the conflict is <c>Open</c> or <c>Deferred</c>. A deferred conflict is still waiting and can be resolved.</param>
/// <param name="Path">The item's path relative to the synchronised root: the remote path, else the local path, else the last reconciled path. Empty when the daemon no longer knows the item.</param>
/// <param name="Local">What the local disk holds; <see langword="null"/> when the file is not there, for example after a local delete or when the daemon could not read it.</param>
/// <param name="Remote">What the cloud holds; <see langword="null"/> when the file is not there, for example after a remote delete or when the daemon could not read it.</param>
public sealed record ConflictView(Guid ConflictId, Guid SyncJobId, Guid ItemId, string Type, DateTimeOffset DetectedUtc, string Status, string Path, ConflictSideView? Local, ConflictSideView? Remote);
