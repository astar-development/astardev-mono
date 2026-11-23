using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>How full the connected drive is, as the provider reports it. A report of remote state only.</summary>
/// <param name="UsedBytes">Storage in use, in bytes.</param>
/// <param name="RemainingBytes">Storage still free, in bytes.</param>
/// <param name="TotalBytes">Storage the account is allowed, in bytes.</param>
/// <param name="ItemCountEstimate">Approximate number of items on the drive; none when the provider has no such figure, as OneDrive has none.</param>
public sealed record ProviderDriveInfo(long UsedBytes, long RemainingBytes, long TotalBytes, Option<long> ItemCountEstimate);
