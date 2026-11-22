namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class InstantTimeProvider : TimeProvider
{
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => base.CreateTimer(callback, state, TimeSpan.Zero, period);
}
