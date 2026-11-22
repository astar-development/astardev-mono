using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class QueueRecoveryStageShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task requeue_running_operations()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-recovery", Token);
        var interrupted = await host.AddOperationAsync(job, OperationStatus.Running, Token);

        var result = await StageFrom(host).ExecuteAsync(Token);
        var listed = (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

        result.Value.ShouldBeOfType<Ok<Unit>>();
        listed.Single(operation => operation.Id == interrupted.Id).Status.ShouldBe(OperationStatus.Pending);
    }

    [Fact]
    public async Task leave_dead_letters_alone()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-recovery", Token);
        var dead = await host.AddOperationAsync(job, OperationStatus.DeadLetter, Token);

        await StageFrom(host).ExecuteAsync(Token);
        var listed = (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

        listed.Single(operation => operation.Id == dead.Id).Status.ShouldBe(OperationStatus.DeadLetter);
    }

    [Fact]
    public async Task skip_a_database_in_recovery_mode()
    {
        await using var host = DaemonTestHost.Create(InfrastructureInitialisationStageShould.CorruptDatabaseAt);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);

        var result = await StageFrom(host).ExecuteAsync(Token);

        host.Services.GetRequiredService<IDatabaseState>().Mode.ShouldBe(DatabaseMode.Recovery);
        result.Value.ShouldBeOfType<Ok<Unit>>();
    }

    [Fact]
    public async Task fail_startup_when_the_queue_cannot_be_written()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        var result = await StageFrom(host).ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Fail<StartupError>>().Error.StageName.ShouldBe("Queue recovery");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => StageFrom(host).ExecuteAsync(cancelled.Token));
    }

    private static IStartupStage StageFrom(DaemonTestHost host) => host.Services.GetServices<IStartupStage>().OfType<QueueRecoveryStage>().Single();
}
