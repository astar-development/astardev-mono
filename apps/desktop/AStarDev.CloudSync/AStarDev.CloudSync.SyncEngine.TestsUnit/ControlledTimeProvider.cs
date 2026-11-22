namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

internal sealed class ControlledTimeProvider : TimeProvider
{
    private readonly List<ControlledTimer> timers = [];
    private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public int PendingTimers
    {
        get
        {
            lock (timers) return timers.Count(timer => timer.Pending);
        }
    }

    public override DateTimeOffset GetUtcNow() => now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ControlledTimer(callback, state, now + dueTime);
        lock (timers) timers.Add(timer);

        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        now += elapsed;
        ControlledTimer[] due;
        lock (timers) due = [.. timers.Where(timer => timer.Pending && timer.DueAt <= now)];

        foreach (var timer in due) timer.Fire();
    }

    private sealed class ControlledTimer(TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
    {
        public bool Pending { get; private set; } = true;

        public DateTimeOffset DueAt { get; } = dueAt;

        public void Fire()
        {
            Pending = false;
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose() => Pending = false;

        public ValueTask DisposeAsync()
        {
            Pending = false;

            return ValueTask.CompletedTask;
        }
    }
}
