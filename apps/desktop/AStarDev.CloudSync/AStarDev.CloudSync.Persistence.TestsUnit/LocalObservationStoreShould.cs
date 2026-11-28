using System.Data.Common;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class LocalObservationStoreShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] Paths = ["doc", "docs", "docs!x", "docs-old/y", "docs.txt", "docs/a.txt", "docs/sub", "docs/sub/b.txt", "docs/sub2/c.txt", "docs0", "docs2/x", "docs\u00e9/z", "other/docs/z"];

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
    public async Task list_only_the_latest_snapshot_of_each_file_seen_several_times()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var store = host.Services.GetRequiredService<ILocalObservationStore>();
        var changed = new LocalSnapshot(new RelativePath("a.txt"), 5, Stamp.AddSeconds(1), null, new ContentFingerprint("a2"));
        var recreated = new LocalSnapshot(new RelativePath("c.txt"), 7, Stamp.AddSeconds(2), null, null);
        await store.RecordAsync(job.Id, [new LocalSnapshot(new RelativePath("a.txt"), 3, Stamp, null, new ContentFingerprint("a1")), new LocalSnapshot(new RelativePath("b.txt"), 4, Stamp, null, null), new LocalSnapshot(new RelativePath("c.txt"), 6, Stamp, null, null)], [], Stamp, Token);
        await store.RecordAsync(job.Id, [changed], [new RelativePath("b.txt"), new RelativePath("c.txt")], Stamp.AddSeconds(1), Token);
        await store.RecordAsync(job.Id, [recreated], [], Stamp.AddSeconds(2), Token);

        var listed = await store.ListPresentAsync(job.Id, default, Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<LocalSnapshot>>>().Value.OrderBy(snapshot => snapshot.Path.Value).ShouldBe([changed, recreated]);
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

    [Theory]
    [InlineData("", new[] { "doc", "docs", "docs!x", "docs-old/y", "docs.txt", "docs/a.txt", "docs/sub", "docs/sub/b.txt", "docs/sub2/c.txt", "docs0", "docs2/x", "docs\u00e9/z", "other/docs/z" })]
    [InlineData("docs", new[] { "docs", "docs/a.txt", "docs/sub", "docs/sub/b.txt", "docs/sub2/c.txt" })]
    [InlineData("docs/sub", new[] { "docs/sub", "docs/sub/b.txt" })]
    [InlineData("doc", new[] { "doc" })]
    [InlineData("missing", new string[0])]
    public async Task list_only_the_subtree_folder_and_the_paths_under_it(string subtree, string[] expected)
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var store = host.Services.GetRequiredService<ILocalObservationStore>();
        await store.RecordAsync(job.Id, [.. Paths.Select(path => new LocalSnapshot(new RelativePath(path), 1, Stamp, null, null))], [], Stamp, Token);

        var listed = await store.ListPresentAsync(job.Id, new RelativePath(subtree), Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<LocalSnapshot>>>().Value.Select(snapshot => snapshot.Path.Value).Order(StringComparer.Ordinal).ShouldBe(expected);
    }

    [Fact]
    public async Task find_the_subtree_through_the_local_path_index()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);
        await using var command = LocalObservationStore.ItemsUnder(context, job.Id, new RelativePath("docs")).CreateDbCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + command.CommandText;
        await context.Database.OpenConnectionAsync(Token);

        var plan = await QueryPlanAsync(command);

        plan.ShouldContain(detail => detail.Contains("USING INDEX IX_Items_SyncJobId_LocalPath (SyncJobId=? AND LocalPath>? AND LocalPath<?)"));
        plan.ShouldNotContain(detail => detail.StartsWith("SCAN"));
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

    private static async Task<List<string>> QueryPlanAsync(DbCommand command)
    {
        var plan = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Token);
        while (await reader.ReadAsync(Token))
            plan.Add(reader.GetString(3));

        return plan;
    }
}
