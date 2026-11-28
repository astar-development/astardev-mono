using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class KnownSnapshotStoreShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_only_the_baselines_of_the_requested_items()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = (await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge)).Id;
        var wanted = await CommitAsync(host, job, "a.txt");
        await CommitAsync(host, job, "b.txt");

        var listed = await host.Services.GetRequiredService<IKnownSnapshotStore>().ListAsync(job, [wanted.ItemId, new ItemId(Guid.NewGuid())], Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<KnownEntry>>>().Value.ShouldBe([wanted]);
    }

    [Fact]
    public async Task honour_cancellation_when_given_item_ids()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IKnownSnapshotStore>().ListAsync(new SyncJobId(Guid.NewGuid()), [new ItemId(Guid.NewGuid())], cancelled.Token));
    }

    private static async Task<KnownEntry> CommitAsync(PersistenceTestHost host, SyncJobId job, string path)
    {
        var entry = new KnownEntry(new ItemId(Guid.NewGuid()), new KnownSnapshot(new RelativePath(path), 10, Stamp, null, null));
        var operation = new OperationId(Guid.NewGuid());
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();
        await known.StageAsync(job, operation, entry.ItemId, entry.Snapshot, Token);
        await known.CommitAsync(operation, Token);

        return entry;
    }
}
