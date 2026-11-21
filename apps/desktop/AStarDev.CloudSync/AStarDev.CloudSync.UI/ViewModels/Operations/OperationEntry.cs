namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>One recorded operation, as the daemon will eventually report it. Views never show the raw values; <see cref="OperationRowViewModel"/> formats them.</summary>
/// <param name="Id">Stable identifier, usable as a navigation target.</param>
/// <param name="Provider">Display name of the provider the operation ran against.</param>
/// <param name="Type">What the operation did.</param>
/// <param name="Status">Where the operation is; the outcome once it is <see cref="OperationStatus.Completed"/> or <see cref="OperationStatus.Failed"/>.</param>
/// <param name="Timestamp">When the operation was recorded, with the offset it was recorded in.</param>
/// <param name="Path">The affected path, relative to the sync root.</param>
/// <param name="SizeBytes">Size of the affected item in bytes; zero when not applicable.</param>
internal sealed record OperationEntry(string Id, string Provider, OperationType Type, OperationStatus Status, DateTimeOffset Timestamp, string Path, long SizeBytes);
