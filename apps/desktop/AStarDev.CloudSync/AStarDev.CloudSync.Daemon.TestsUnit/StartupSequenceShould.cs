using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.FunctionalParadigm;
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

        var result = await sequence.RunAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Ok<Unit>>();
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

        var result = await sequence.RunAsync(TestContext.Current.CancellationToken);

        var error = result.Value.ShouldBeOfType<Fail<StartupError>>().Error;
        error.StageName.ShouldBe("two");
        error.Cause.ShouldBe("boom");
        ran.ShouldBe(["one", "two"]);
    }

    [Fact]
    public async Task stop_when_cancelled()
    {
        using var source = new CancellationTokenSource();
        var ran = new List<string>();
        var sequence = new StartupSequence(
            [new FakeStage("one", ran, onExecute: source.Cancel), new FakeStage("two", ran)],
            NullLogger<StartupSequence>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() => sequence.RunAsync(source.Token));

        ran.ShouldBe(["one"]);
    }

    [Fact]
    public async Task not_start_any_stage_when_already_cancelled()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var ran = new List<string>();
        var sequence = new StartupSequence([new FakeStage("one", ran)], NullLogger<StartupSequence>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() => sequence.RunAsync(source.Token));

        ran.ShouldBeEmpty();
    }

    [Fact]
    public async Task pass_the_token_to_every_stage()
    {
        using var source = new CancellationTokenSource();
        var ran = new List<string>();
        var first = new FakeStage("one", ran);
        var second = new FakeStage("two", ran);
        var sequence = new StartupSequence([first, second], NullLogger<StartupSequence>.Instance);

        await sequence.RunAsync(source.Token);

        first.ReceivedToken.ShouldBe(source.Token);
        second.ReceivedToken.ShouldBe(source.Token);
    }
}
