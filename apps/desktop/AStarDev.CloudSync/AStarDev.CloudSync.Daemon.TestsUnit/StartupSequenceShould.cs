using AStarDev.CloudSync.Daemon.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class StartupSequenceShould
{
    [Fact]
    public async Task run_stages_in_the_documented_order()
    {
        var ran = new List<string>();
        using var provider = new ServiceCollection().AddStartupStages().BuildServiceProvider();
        var stages = provider.GetServices<IStartupStage>().Select(stage => new FakeStage(stage.Name, ran)).ToList();
        var sequence = new StartupSequence(stages, NullLogger<StartupSequence>.Instance);

        await sequence.RunAsync(TestContext.Current.CancellationToken);

        ran.ShouldBe(
        [
            "Process start",
            "Infrastructure initialisation",
            "State recovery",
            "Queue recovery",
            "Background services",
            "Accept connections"
        ]);
    }

    [Fact]
    public async Task stop_and_report_when_an_early_stage_fails()
    {
        var ran = new List<string>();
        var sequence = new StartupSequence(
            [new FakeStage("one", ran), new FakeStage("two", ran, fails: true), new FakeStage("three", ran)],
            NullLogger<StartupSequence>.Instance);

        var error = await Should.ThrowAsync<StartupFailedException>(() => sequence.RunAsync(TestContext.Current.CancellationToken));

        error.StageName.ShouldBe("two");
        error.InnerException.ShouldBeOfType<InvalidOperationException>();
        ran.ShouldBe(["one", "two"]);
    }

    private sealed class FakeStage(string name, List<string> ran, bool fails = false) : IStartupStage
    {
        public string Name { get; } = name;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            ran.Add(Name);
            return fails ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        }
    }
}
