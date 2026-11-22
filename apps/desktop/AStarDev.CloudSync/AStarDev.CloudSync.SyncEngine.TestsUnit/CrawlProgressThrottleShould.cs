namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class CrawlProgressThrottleShould
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    [Fact]
    public void emit_at_most_one_event_per_interval()
    {
        var clock = new ManualTimeProvider();
        var throttle = new CrawlProgressThrottle(Interval, clock);
        var admitted = 0;

        for (var tick = 0; tick < 100; tick++)
        {
            if (throttle.TryAdmit()) admitted++;
            clock.Advance(TimeSpan.FromMilliseconds(10));
        }

        admitted.ShouldBe(2);
    }

    [Fact]
    public void admit_the_first_event_at_once()
        => new CrawlProgressThrottle(Interval, new ManualTimeProvider()).TryAdmit().ShouldBeTrue();

    [Fact]
    public void admit_again_once_the_interval_has_passed()
    {
        var clock = new ManualTimeProvider();
        var throttle = new CrawlProgressThrottle(Interval, clock);
        throttle.TryAdmit();

        clock.Advance(Interval);

        throttle.TryAdmit().ShouldBeTrue();
    }
}
