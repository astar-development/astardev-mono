namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan elapsed) => now += elapsed;
}
