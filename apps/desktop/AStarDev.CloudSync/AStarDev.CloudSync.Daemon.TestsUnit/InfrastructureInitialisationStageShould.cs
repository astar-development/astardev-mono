using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.Persistence;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class InfrastructureInitialisationStageShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task prepare_the_database_when_it_runs()
    {
        await using var host = DaemonTestHost.Create();
        var stage = StageFrom(host);

        var result = await stage.ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        host.Services.GetRequiredService<IDatabaseState>().Mode.ShouldBe(DatabaseMode.Normal);
        File.Exists(host.DatabasePath).ShouldBeTrue();
    }

    [Fact]
    public async Task succeed_in_recovery_mode_so_the_daemon_can_report_it()
    {
        await using var host = DaemonTestHost.Create(CorruptDatabaseAt);
        var stage = StageFrom(host);

        var result = await stage.ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        host.Services.GetRequiredService<IDatabaseState>().Mode.ShouldBe(DatabaseMode.Recovery);
    }

    [Fact]
    public async Task fail_startup_when_the_database_cannot_be_reached()
    {
        var blocker = Path.Combine(Path.GetTempPath(), $"cloudsync-blocker-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(blocker, "a file where a folder is needed", Token);
        var databasePath = Path.Combine(blocker, "cloudsync.db");
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration["Database:Path"] = databasePath;
        builder.Services.AddCloudSyncDaemon();
        using var host = builder.Build();
        var stage = host.Services.GetServices<IStartupStage>().OfType<InfrastructureInitialisationStage>().Single();

        var result = await stage.ExecuteAsync(Token);
        File.Delete(blocker);

        var error = result.Value.ShouldBeOfType<Fail<StartupError>>().Error;
        error.StageName.ShouldBe("Infrastructure initialisation");
        error.Cause.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.Create();
        var stage = StageFrom(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => stage.ExecuteAsync(cancelled.Token));
    }

    internal static void CorruptDatabaseAt(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, new string('x', 8192));
    }

    private static InfrastructureInitialisationStage StageFrom(DaemonTestHost host)
        => host.Services.GetServices<IStartupStage>().OfType<InfrastructureInitialisationStage>().Single();
}
