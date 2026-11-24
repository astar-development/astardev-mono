using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Classification.TestsUnit.SyncTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassifyingItemIndexStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task link_files_after_a_page_is_saved()
    {
        await using var host = await SeededHostAsync();
        var scope = await AddScopeAsync(host);

        var result = await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [File("a", "Holiday/beach.jpg"), File("b", "Work/notes.txt")], CrawlCheckpoint.None, Now, Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        (await FilesFor(host, "jpg")).Count.ShouldBe(1);
        (await FilesFor(host, "holiday")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task not_classify_a_folder()
    {
        await using var host = await SeededHostAsync();
        var scope = await AddScopeAsync(host);

        await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [new ProviderItem(new RemoteItemId("a"), new RelativePath("Holiday"), ItemType.Folder, 0, Now)], CrawlCheckpoint.None, Now, Token);

        (await FilesFor(host, "holiday")).ShouldBeEmpty();
    }

    [Fact]
    public async Task classify_a_renamed_file_by_its_new_path()
    {
        await using var host = await SeededHostAsync();
        var scope = await AddScopeAsync(host);
        var index = host.Services.GetRequiredService<IItemIndexStore>();
        await index.SavePageAsync(scope, [File("a", "Work/beach.jpg")], CrawlCheckpoint.None, Now, Token);

        await index.ApplyDeltaPageAsync(scope, [new ProviderChange(ChangeKind.Renamed, new RemoteItemId("a"), File("a", "Holiday/beach.jpg"))], null, Now, Token);

        (await FilesFor(host, "holiday")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task remove_the_links_of_a_deleted_item()
    {
        await using var host = await SeededHostAsync();
        var scope = await AddScopeAsync(host);
        var index = host.Services.GetRequiredService<IItemIndexStore>();
        await index.SavePageAsync(scope, [File("a", "Holiday/beach.jpg")], CrawlCheckpoint.None, Now, Token);

        await index.ApplyDeltaPageAsync(scope, [new ProviderChange(ChangeKind.Deleted, new RemoteItemId("a"))], null, Now, Token);

        (await FilesFor(host, "jpg")).ShouldBeEmpty();
    }

    [Fact]
    public async Task return_the_inner_result_when_the_vocabulary_cannot_be_loaded()
    {
        var failing = new CountingVocabularyProvider(new Fail<ClassificationError>(new StorageFailed("down")), 0);
        await using var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationVocabularyProvider>(failing));
        var scope = await AddScopeAsync(host);

        var result = await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [File("a", "Holiday/beach.jpg")], CrawlCheckpoint.None, Now, Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        failing.Loads.ShouldBe(1);
    }

    [Fact]
    public async Task classify_again_when_the_vocabulary_changes_while_a_page_is_classified()
    {
        var provider = new CountingVocabularyProvider(new Ok<ClassificationVocabulary>(ClassificationVocabulary.Create([], [])), 2);
        await using var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationVocabularyProvider>(provider));
        var scope = await AddScopeAsync(host);

        await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [File("a", "Holiday/beach.jpg")], CrawlCheckpoint.None, Now, Token);

        provider.Loads.ShouldBe(3);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await SeededHostAsync();
        var scope = await AddScopeAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [File("a", "Holiday/beach.jpg")], CrawlCheckpoint.None, Now, cancelled.Token));
    }

    private static async Task<ClassificationTestHost> SeededHostAsync()
    {
        var seed = FixedSeedSource.Seed("1", new SeedTerm("jpg", "Colour"), new SeedTerm("holiday", "Place"));
        var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationSeedSource>(new FixedSeedSource(new Ok<ClassificationSeed>(seed))));
        await host.Services.GetRequiredService<ClassificationSeeder>().SeedAsync(Token);

        return host;
    }

    private static Task<SyncScope> AddScopeAsync(ClassificationTestHost host) => SyncTestData.AddScopeAsync(host, Token);

    private static async Task<IReadOnlyList<ItemId>> FilesFor(ClassificationTestHost host, string term)
    {
        var terms = (await host.Services.GetRequiredService<ITermStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value;

        return (await host.Services.GetRequiredService<IFileClassificationStore>().GetItemsForTermAsync(terms.Single(candidate => candidate.Text == term).Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<ItemId>>>().Value;
    }
}
