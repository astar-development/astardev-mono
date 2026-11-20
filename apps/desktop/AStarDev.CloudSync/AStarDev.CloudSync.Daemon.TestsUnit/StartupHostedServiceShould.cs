using AStarDev.CloudSync.Daemon.Startup;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class StartupHostedServiceShould
{
    [Fact]
    public async Task fail_the_host_start_when_a_stage_fails()
    {
        var ran = new List<string>();
        var sequence = new StartupSequence([new FakeStage("one", ran, fails: true)], NullLogger<StartupSequence>.Instance);
        var service = new StartupHostedService(sequence);

        var exception = await Should.ThrowAsync<StartupFailedException>(() => service.StartAsync(TestContext.Current.CancellationToken));

        exception.Error.StageName.ShouldBe("one");
    }

    [Fact]
    public async Task start_when_every_stage_succeeds()
    {
        var ran = new List<string>();
        var sequence = new StartupSequence([new FakeStage("one", ran)], NullLogger<StartupSequence>.Instance);
        var service = new StartupHostedService(sequence);

        await service.StartAsync(TestContext.Current.CancellationToken);

        ran.ShouldBe(["one"]);
    }

    [Fact]
    public async Task propagate_cancellation_instead_of_failing_the_start()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var ran = new List<string>();
        var sequence = new StartupSequence([new FakeStage("one", ran)], NullLogger<StartupSequence>.Instance);
        var service = new StartupHostedService(sequence);

        await Should.ThrowAsync<OperationCanceledException>(() => service.StartAsync(source.Token));

        ran.ShouldBeEmpty();
    }
}
