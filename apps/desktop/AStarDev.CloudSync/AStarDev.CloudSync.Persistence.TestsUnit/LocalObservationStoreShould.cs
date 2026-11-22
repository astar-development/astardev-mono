using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class LocalObservationStoreShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task round_trip_the_local_identity()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var store = host.Services.GetRequiredService<ILocalObservationStore>();
        var snapshot = new LocalSnapshot(new RelativePath("a.txt"), 3, Stamp, new LocalIdentity("2049:1234"), new ContentFingerprint("fp"));

        await store.RecordAsync(job.Id, [snapshot], [], Stamp, Token);
        var listed = await store.ListPresentAsync(job.Id, default, Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<LocalSnapshot>>>().Value.ShouldBe([snapshot]);
    }

    [Fact]
    public async Task keep_the_identity_of_a_deleted_file_for_rename_detection()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var store = host.Services.GetRequiredService<ILocalObservationStore>();
        var snapshot = new LocalSnapshot(new RelativePath("a.txt"), 3, Stamp, new LocalIdentity("2049:1234"), null);
        await store.RecordAsync(job.Id, [snapshot], [], Stamp, Token);
        await store.RecordAsync(job.Id, [], [snapshot.Path], Stamp.AddSeconds(1), Token);

        var items = await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job.Id, Token);

        var row = items.Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value.ShouldHaveSingleItem();
        row.Local.ShouldBeNull();
        row.LastLocalIdentity.ShouldBe(new LocalIdentity("2049:1234"));
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ILocalObservationStore>().ListPresentAsync(job.Id, default, cancelled.Token));
    }
}
