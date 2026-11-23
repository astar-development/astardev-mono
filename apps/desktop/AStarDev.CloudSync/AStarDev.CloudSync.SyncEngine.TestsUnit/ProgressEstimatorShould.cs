using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class ProgressEstimatorShould
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    [Fact]
    public void compute_throughput_over_the_window()
    {
        var clock = new ManualTimeProvider();
        var estimator = new ProgressEstimator(Window, clock);
        estimator.Record(0);
        clock.Advance(TimeSpan.FromSeconds(60));
        estimator.Record(600);
        clock.Advance(TimeSpan.FromSeconds(10));
        estimator.Record(1600);

        estimator.ItemsPerSecond().Value.ShouldBeOfType<Some<double>>().Value.ShouldBe(100d);
    }

    [Fact]
    public void estimate_remaining_time_from_throughput_and_total()
    {
        var clock = new ManualTimeProvider();
        var estimator = new ProgressEstimator(Window, clock);
        estimator.Record(0);
        clock.Advance(TimeSpan.FromSeconds(5));
        estimator.Record(500);

        estimator.Remaining(500, Option.Some(1500L)).Value.ShouldBeOfType<Some<TimeSpan>>().Value.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void report_no_estimate_when_the_total_is_unknown()
    {
        var clock = new ManualTimeProvider();
        var estimator = new ProgressEstimator(Window, clock);
        estimator.Record(0);
        clock.Advance(TimeSpan.FromSeconds(5));
        estimator.Record(500);

        estimator.Remaining(500, Option.None<long>()).Value.ShouldBeOfType<None<TimeSpan>>();
    }

    [Fact]
    public void report_no_throughput_before_two_samples()
    {
        var estimator = new ProgressEstimator(Window, new ManualTimeProvider());
        estimator.Record(0);

        estimator.ItemsPerSecond().Value.ShouldBeOfType<None<double>>();
    }
}
