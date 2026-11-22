using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.FunctionalParadigm;
using AStarDev.SingleInstance;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ProcessStartStageShould : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"ps-{Guid.NewGuid():N}"[..11]);
    private readonly ServiceProvider provider = new ServiceCollection().AddLogging().AddSingleInstance().BuildServiceProvider();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProcessStartStage Stage(string? path = null) => new(provider.GetRequiredService<ISingleInstanceGuard>(), SingleInstanceOptionsFactory.Create(path ?? folder, "daemon.sock.instance", TimeSpan.FromMilliseconds(500)));

    [Fact]
    public async Task succeed_for_the_only_daemon()
    {
        await using var stage = Stage();

        var result = await stage.ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
    }

    [Fact]
    public async Task fail_with_a_clear_cause_for_a_second_daemon()
    {
        await using var first = Stage();
        await first.ExecuteAsync(Token);
        await using var second = Stage();

        var result = await second.ExecuteAsync(Token);

        var error = result.Value.ShouldBeOfType<Fail<StartupError>>().Error;
        (error.StageName, error.Cause).ShouldBe(("Process start", ApplicationConstants.AlreadyRunningText));
    }

    [Fact]
    public async Task fail_when_the_instance_lock_cannot_be_created()
    {
        Directory.CreateDirectory(folder);
        var blocker = Path.Combine(folder, "blocker");
        await File.WriteAllTextAsync(blocker, "file", Token);
        await using var stage = Stage(Path.Combine(blocker, "data"));

        var result = await stage.ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Fail<StartupError>>().Error.Cause.ShouldBe(ApplicationConstants.InstanceLockUnavailableText);
    }

    [Fact]
    public async Task release_the_lock_when_disposed_so_the_next_daemon_can_start()
    {
        var first = Stage();
        await first.ExecuteAsync(Token);
        await first.DisposeAsync();
        await using var next = Stage();

        var result = await next.ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
    }

    [Fact]
    public async Task be_safe_to_dispose_when_it_never_held_the_lock()
    {
        var stage = Stage();

        await stage.DisposeAsync();
    }

    [Fact]
    public async Task propagate_cancellation()
    {
        await using var stage = Stage();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => stage.ExecuteAsync(cancellation.Token));
    }

    public void Dispose()
    {
        provider.Dispose();
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}
