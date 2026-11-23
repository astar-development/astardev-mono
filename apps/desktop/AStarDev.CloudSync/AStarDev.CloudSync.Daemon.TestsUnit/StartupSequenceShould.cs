using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class StartupSequenceShould
{
    [Fact]
    public async Task succeed_with_the_stages_the_daemon_registers()
    {
        await using var host = DaemonTestHost.Create();
        var sequence = host.Services.GetRequiredService<StartupSequence>();

        var result = await sequence.RunAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Ok<Unit>>();
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

    [Fact]
    public async Task log_each_stage_through_the_generated_messages()
    {
        var logs = new EventCapturingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var ran = new List<string>();
        var sequence = new StartupSequence([new FakeStage("one", ran), new FakeStage("two", ran, fails: true)], factory.CreateLogger<StartupSequence>());

        await sequence.RunAsync(TestContext.Current.CancellationToken);

        logs.Entries.Select(entry => entry.EventId).ShouldBe([6100, 6102, 6100, 6101]);
    }
}
