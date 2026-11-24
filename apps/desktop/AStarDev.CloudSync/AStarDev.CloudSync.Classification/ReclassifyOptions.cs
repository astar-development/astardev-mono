namespace AStarDev.CloudSync.Classification;

/// <summary>How a reclassify run paces itself.</summary>
/// <param name="PageSize">Files read, classified and written in one batch.</param>
/// <param name="BatchDelay">The pause after each batch, so sync writes get the single SQLite writer between batches.</param>
internal sealed record ReclassifyOptions(int PageSize, TimeSpan BatchDelay)
{
    internal static ReclassifyOptions Default { get; } = new(1000, TimeSpan.FromMilliseconds(25));
}
