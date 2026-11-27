namespace AStarDev.CloudSync.OneDrive.TestsUnit;

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => start;
}
