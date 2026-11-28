using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class ItemIndexStoreShould
{
    private const int Folders = 20;

    private static readonly DateTimeOffset Stamp = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task delete_many_items_of_a_page_in_a_bounded_number_of_commands()
    {
        var counter = new CommandCounter();
        await using var host = PersistenceTestHost.Create(configureServices: services => services.ConfigureDbContext<CloudSyncDbContext>(options => options.AddInterceptors(counter)));
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var scope = await AddScopeAsync(host);
        await AddItemsAsync(host, scope.SyncJobId, Folders);
        var changes = Enumerable.Range(0, Folders).SelectMany(index => new[] { Deleted($"folder{index}"), Deleted($"file{index}") }).Prepend(Modified("file0", "folder0/file0.txt")).ToList();
        counter.Reset();

        var applied = await host.Services.GetRequiredService<IItemIndexStore>().ApplyDeltaPageAsync(scope, changes, null, Stamp, Token);

        var commands = counter.Commands;
        applied.Value.ShouldBeOfType<Ok<Unit>>();
        commands.ShouldBeLessThanOrEqualTo(5);
        (await RemainingAsync(host)).ShouldBe(["kept"]);
    }

    [Fact]
    public async Task forget_only_the_deleted_items_and_their_snapshots()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var scope = await AddScopeAsync(host);
        await AddItemsAsync(host, scope.SyncJobId, 2);
        var changes = new List<ProviderChange> { Modified("file1", "folder1/file1.txt"), new(ChangeKind.Created, new RemoteItemId("fresh"), new ProviderItem(new RemoteItemId("fresh"), new RelativePath("fresh.txt"), ItemType.File, 1, Stamp)), Deleted("folder1"), Deleted("file1") };

        var applied = await host.Services.GetRequiredService<IItemIndexStore>().ApplyDeltaPageAsync(scope, changes, null, Stamp, Token);

        applied.Value.ShouldBeOfType<Ok<Unit>>();
        (await RemainingAsync(host)).ShouldBe(["file0", "folder0", "fresh", "kept"]);
        (await SnapshotCountAsync(host)).ShouldBe(2);
    }

    [Fact]
    public async Task index_an_item_again_when_it_comes_back_after_it_was_deleted_in_the_same_page()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var scope = await AddScopeAsync(host);
        await AddItemsAsync(host, scope.SyncJobId, 1);
        var changes = new List<ProviderChange> { Deleted("file0"), Modified("file0", "file0.txt") };

        var applied = await host.Services.GetRequiredService<IItemIndexStore>().ApplyDeltaPageAsync(scope, changes, null, Stamp, Token);

        applied.Value.ShouldBeOfType<Ok<Unit>>();
        (await RemainingAsync(host)).ShouldBe(["file0", "folder0", "kept"]);
        (await SnapshotCountAsync(host)).ShouldBe(1);
    }

    [Fact]
    public async Task save_a_page_with_more_items_than_sqlite_allows_parameters_twice()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var scope = await AddScopeAsync(host);
        var store = host.Services.GetRequiredService<IItemIndexStore>();
        var items = Enumerable.Range(0, 40_000).Select(index => new ProviderItem(new RemoteItemId($"remote-{index}"), new RelativePath($"f{index}.txt"), ItemType.File, index, Stamp)).ToList();

        var first = await store.SavePageAsync(scope, items, new CrawlCheckpoint(null, items.Count, true, null), Stamp, Token);
        var second = await store.SavePageAsync(scope, items, new CrawlCheckpoint(null, items.Count, true, null), Stamp.AddSeconds(1), Token);

        first.Value.ShouldBeOfType<Ok<Unit>>();
        second.Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static ProviderChange Modified(string remoteId, string path) => new(ChangeKind.Modified, new RemoteItemId(remoteId), new ProviderItem(new RemoteItemId(remoteId), new RelativePath(path), ItemType.File, 99, Stamp));

    private static ProviderChange Deleted(string remoteId) => new(ChangeKind.Deleted, new RemoteItemId(remoteId));

    private static async Task<SyncScope> AddScopeAsync(PersistenceTestHost host)
    {
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var scope = new SyncScope(job.Id, new RemoteItemId("root"));
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, Token);

        return scope;
    }

    private static async Task AddItemsAsync(PersistenceTestHost host, SyncJobId jobId, int folders)
    {
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);
        context.Items.Add(Item(jobId, ItemType.File, "kept", "kept.txt"));
        foreach (var index in Enumerable.Range(0, folders))
        {
            var folder = Item(jobId, ItemType.Folder, $"folder{index}", $"folder{index}");
            var file = Item(jobId, ItemType.File, $"file{index}", $"folder{index}/file{index}.txt");
            context.Items.AddRange(folder, file);
            context.ItemSnapshots.Add(new ItemSnapshotEntity { Id = new SnapshotId(Guid.NewGuid()), ItemId = file.Id, SnapshotSource = SnapshotSource.Remote, Size = 10, ModifiedUtc = Stamp, ObservedUtc = Stamp });
        }

        await context.SaveChangesAsync(Token);
    }

    private static ItemEntity Item(SyncJobId jobId, ItemType type, string remoteId, string path)
        => new() { Id = new ItemId(Guid.NewGuid()), SyncJobId = jobId, ItemType = type, RemoteItemId = new RemoteItemId(remoteId), LocalPath = new RelativePath(path), CreatedUtc = Stamp, UpdatedUtc = Stamp };

    private static async Task<List<string>> RemainingAsync(PersistenceTestHost host)
    {
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);

        return [.. (await context.Items.Select(item => item.RemoteItemId).ToListAsync(Token)).Select(id => id.Value).Order(StringComparer.Ordinal)];
    }

    private static async Task<int> SnapshotCountAsync(PersistenceTestHost host)
    {
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);

        return await context.ItemSnapshots.CountAsync(Token);
    }
}
