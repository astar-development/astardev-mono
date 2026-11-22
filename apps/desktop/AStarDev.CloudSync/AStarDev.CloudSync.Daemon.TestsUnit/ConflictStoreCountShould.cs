using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ConflictStoreCountShould : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);

    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-conflict-count-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task count_open_and_deferred_conflicts_but_not_resolved_ones()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, localRoot, Token, enabled: false);
        var store = host.Services.GetRequiredService<IConflictStore>();
        foreach (var status in new[] { ConflictStatus.Open, ConflictStatus.Deferred, ConflictStatus.Resolved })
            await store.AddAsync(new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.ModifyModify, status, Stamp), Token);

        var count = await store.CountOpenAsync(Token);

        count.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(2);
    }

    [Fact]
    public async Task honour_cancellation_when_counting()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await host.Services.GetRequiredService<IConflictStore>().CountOpenAsync(source.Token));
    }
}
