using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class CanonicalStateReaderShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task read_only_the_requested_items_as_the_whole_job_read_shows_them()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/tmp/cloudsync-reader", Token);
        var local = new[] { "a.txt", "b.txt", "c.txt" }.Select(path => new LocalSnapshot(new RelativePath(path), 42, Stamp, null, null)).ToList();
        await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job.Id, local, [], Stamp, Token);
        var orphan = new ItemId(Guid.NewGuid());
        var operation = new OperationId(Guid.NewGuid());
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();
        await known.StageAsync(job.Id, operation, orphan, new KnownSnapshot(new RelativePath("gone.txt"), 7, Stamp, null, null), Token);
        await known.CommitAsync(operation, Token);
        var reader = host.Services.GetRequiredService<CanonicalStateReader>();
        var whole = (await reader.ReadAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<CanonicalItemState>>>().Value;
        var wanted = new[] { whole.Single(state => state.Local?.Path.Value == "a.txt").ItemId, orphan };

        var read = await reader.ReadAsync(job.Id, wanted, Token);

        read.Value.ShouldBeOfType<Ok<IReadOnlyList<CanonicalItemState>>>().Value.OrderBy(state => state.ItemId.Value).ShouldBe(whole.Where(state => wanted.Contains(state.ItemId)).OrderBy(state => state.ItemId.Value));
    }

    [Fact]
    public async Task honour_cancellation_when_given_item_ids()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<CanonicalStateReader>().ReadAsync(new SyncJobId(Guid.NewGuid()), [new ItemId(Guid.NewGuid())], cancelled.Token));
    }
}
