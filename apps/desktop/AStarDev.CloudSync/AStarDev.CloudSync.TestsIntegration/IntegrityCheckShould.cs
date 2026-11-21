using AStarDev.CloudSync.Persistence;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class IntegrityCheckShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task enter_recovery_mode_on_a_corrupt_database()
    {
        await using var host = DatabaseTestHost.Create();
        var corrupt = new string('x', 8192);
        Directory.CreateDirectory(Path.GetDirectoryName(host.DatabasePath)!);
        await File.WriteAllTextAsync(host.DatabasePath, corrupt, Token);

        var result = await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var state = host.Services.GetRequiredService<IDatabaseState>();

        result.Value.ShouldBeOfType<Ok<Unit>>();
        state.Mode.ShouldBe(DatabaseMode.Recovery);
        state.RecoveryReason.ShouldNotBeNullOrWhiteSpace();
        (await File.ReadAllTextAsync(host.DatabasePath, Token)).ShouldBe(corrupt);
    }

    [Fact]
    public async Task stay_in_normal_mode_on_a_healthy_database()
    {
        await using var host = DatabaseTestHost.Create();

        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var state = host.Services.GetRequiredService<IDatabaseState>();

        state.Mode.ShouldBe(DatabaseMode.Normal);
        state.RecoveryReason.ShouldBeNull();
    }
}
