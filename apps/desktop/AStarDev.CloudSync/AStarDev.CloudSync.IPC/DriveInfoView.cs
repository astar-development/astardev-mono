namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetDriveInfo"/>: the storage facts the provider reports for the connected drive.</summary>
/// <param name="UsedBytes">Storage in use, in bytes.</param>
/// <param name="RemainingBytes">Storage still free, in bytes.</param>
/// <param name="TotalBytes">Storage the account is allowed, in bytes.</param>
/// <param name="ItemCountEstimate">Approximate number of items on the drive; <see langword="null"/> when the provider has no such figure, as OneDrive has none.</param>
public sealed record DriveInfoView(long UsedBytes, long RemainingBytes, long TotalBytes, long? ItemCountEstimate);
