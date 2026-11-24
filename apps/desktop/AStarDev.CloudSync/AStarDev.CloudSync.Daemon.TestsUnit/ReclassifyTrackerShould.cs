using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static AStarDev.CloudSync.Daemon.TestsUnit.ClassificationDaemonTestData;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ReclassifyTrackerShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task publish_progress_and_a_last_event_when_the_run_ends()
    {
        var server = new RecordingIpcServer();
        await using var host = await SeededHostAsync(Token, services => services.RemoveAll<IIpcServer>().AddSingleton<IIpcServer>(server));
        await SaveFilesAsync(host, Token, "Photos/a.jpg");

        var outcome = await host.Services.GetRequiredService<ReclassifyTracker>().RunAsync(progress => host.Services.GetRequiredService<IClassificationReclassifier>().StartAsync(progress, Token), Token);

        outcome.Value.ShouldBeOfType<ReclassifyCompleted>();
        var published = server.Published.OfType<ReclassifyProgressChanged>().ToList();
        published.Last().ShouldBe(new ReclassifyProgressChanged(false, 1));
        published.Take(published.Count - 1).ShouldAllBe(progress => progress.Running);
    }

    [Fact]
    public async Task refuse_a_second_run_while_one_is_active()
    {
        await using var host = await SeededHostAsync(Token);
        var tracker = host.Services.GetRequiredService<ReclassifyTracker>();
        var release = new TaskCompletionSource<ReclassifyOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = tracker.RunAsync(_ => release.Task, Token);

        var second = await tracker.RunAsync(_ => release.Task, Token);
        tracker.Running.ShouldBeTrue();
        release.SetResult(new ReclassifyCompleted(0));
        await first;

        second.Value.ShouldBeOfType<ReclassifyAlreadyRunning>();
        tracker.Running.ShouldBeFalse();
    }

    [Fact]
    public async Task answer_a_start_command_at_once_and_reject_it_while_a_run_is_active()
    {
        await using var host = await SeededHostAsync(Token);
        var tracker = host.Services.GetRequiredService<ReclassifyTracker>();
        var handler = host.Services.GetRequiredService<StartReclassifyHandler>();
        var release = new TaskCompletionSource<ReclassifyOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = tracker.RunAsync(_ => release.Task, Token);

        var rejected = await handler.HandleAsync(new StartReclassify(), Token);
        release.SetResult(new ReclassifyCompleted(0));
        await held;
        var accepted = await handler.HandleAsync(new StartReclassify(), Token);

        rejected.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Rejected);
        accepted.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
    }

    [Fact]
    public async Task report_the_status_with_the_last_finished_run()
    {
        await using var host = await SeededHostAsync(Token);
        var before = (await host.Services.GetRequiredService<ReclassifyStatusQuery>().HandleAsync(new GetReclassifyStatus(), Token)).Value.ShouldBeOfType<Ok<ReclassifyStatusView>>().Value;
        await host.Services.GetRequiredService<IClassificationReclassifier>().StartAsync(new Progress<ReclassifyProgress>(), Token);

        var after = (await host.Services.GetRequiredService<ReclassifyStatusQuery>().HandleAsync(new GetReclassifyStatus(), Token)).Value.ShouldBeOfType<Ok<ReclassifyStatusView>>().Value;

        before.LastCompletedUtc.ShouldBeNull();
        after.LastCompletedUtc.ShouldNotBeNull();
        after.Running.ShouldBeFalse();
    }
}
