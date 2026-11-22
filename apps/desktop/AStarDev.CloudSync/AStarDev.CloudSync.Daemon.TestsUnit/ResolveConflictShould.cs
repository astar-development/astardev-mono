using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ResolveConflictShould : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);

    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-resolve-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task accept_a_deferred_conflict_and_keep_it_open()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var conflict = await AddConflictAsync(host);

        var result = await host.Services.GetRequiredService<ResolveConflictHandler>().HandleAsync(new ResolveConflict([conflict.Id.Value], ConflictChoice.Defer), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.ShouldBe(new CommandResult(CommandOutcome.Accepted));
        (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(conflict.SyncJobId, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task reject_a_conflict_that_does_not_exist()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<ResolveConflictHandler>().HandleAsync(new ResolveConflict([Guid.NewGuid()], ConflictChoice.KeepBoth), Token);

        var answer = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        answer.Outcome.ShouldBe(CommandOutcome.Rejected);
        answer.Message.ShouldBe("That conflict no longer exists.");
    }

    [Fact]
    public async Task reject_a_request_without_conflicts_as_a_validation_error()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        var result = await host.Services.GetRequiredService<ResolveConflictHandler>().HandleAsync(new ResolveConflict([], ConflictChoice.KeepLocal), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.ValidationError);
    }

    [Fact]
    public async Task be_mapped_so_a_client_can_send_it()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var conflict = await AddConflictAsync(host);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var result = await client.SendAsync(new ResolveConflict([conflict.Id.Value], ConflictChoice.Defer), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ResolveConflictHandler>().HandleAsync(new ResolveConflict([Guid.NewGuid()], ConflictChoice.KeepBoth), cancelled.Token));
    }

    private async Task<Conflict> AddConflictAsync(DaemonTestHost host)
    {
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, localRoot, Token);
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.ModifyModify, ConflictStatus.Open, Stamp);
        (await host.Services.GetRequiredService<IConflictStore>().AddAsync(conflict, Token)).Value.ShouldBeOfType<Ok<bool>>().Value.ShouldBeTrue();

        return conflict;
    }
}
