namespace AStarDev.CloudSync.OneDrive;

/// <summary>A Graph drive quota, in bytes.</summary>
/// <param name="Total">Storage the account is allowed.</param>
/// <param name="Used">Storage in use.</param>
/// <param name="Remaining">Storage still free.</param>
internal sealed record GraphQuota(long Total, long Used, long Remaining);
