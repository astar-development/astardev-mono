using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Persistence.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class FileClassificationStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task replace_the_automatic_links_of_an_item()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var blue = await AddTermAsync(host, colour.Id, "blue", Token);
        var items = await AddItemsAsync(host, 1, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();
        await store.ReplaceAutoLinksAsync([new ItemTermLinks(items[0], [red.Id])], Token);

        await store.ReplaceAutoLinksAsync([new ItemTermLinks(items[0], [blue.Id])], Token);

        Ok(await store.GetTermsForItemAsync(items[0], Token)).ShouldBe([blue.Id]);
    }

    [Fact]
    public async Task keep_user_links_when_replacing_automatic_links()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var blue = await AddTermAsync(host, colour.Id, "blue", Token);
        var items = await AddItemsAsync(host, 1, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();
        await store.AssignUserLinkAsync(items[0], red.Id, Token);

        await store.ReplaceAutoLinksAsync([new ItemTermLinks(items[0], [blue.Id])], Token);
        await store.ReplaceAutoLinksAsync([new ItemTermLinks(items[0], [])], Token);

        Ok(await store.GetTermsForItemAsync(items[0], Token)).ShouldBe([red.Id]);
    }

    [Fact]
    public async Task leave_a_user_link_a_user_link_when_classification_finds_the_same_term()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var items = await AddItemsAsync(host, 1, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();
        await store.AssignUserLinkAsync(items[0], red.Id, Token);

        await store.ReplaceAutoLinksAsync([new ItemTermLinks(items[0], [red.Id])], Token);
        await store.ReplaceAutoLinksAsync([new ItemTermLinks(items[0], [])], Token);

        Ok(await store.GetTermsForItemAsync(items[0], Token)).ShouldBe([red.Id]);
    }

    [Fact]
    public async Task skip_an_item_that_is_no_longer_stored()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var items = await AddItemsAsync(host, 1, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();

        var result = await store.ReplaceAutoLinksAsync([new ItemTermLinks(new ItemId(Guid.NewGuid()), [red.Id]), new ItemTermLinks(items[0], [red.Id])], Token);

        Ok(result).ShouldBe(default);
        Ok(await store.GetItemsForTermAsync(red.Id, Token)).ShouldBe([items[0]]);
    }

    [Fact]
    public async Task write_large_updates_in_batches()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var items = await AddItemsAsync(host, 2500, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();

        await store.ReplaceAutoLinksAsync([.. items.Select(item => new ItemTermLinks(item, [red.Id]))], Token);

        Ok(await store.GetFileCountsAsync(Token)).ShouldBe(new Dictionary<TermId, int> { [red.Id] = 2500 });
    }

    [Fact]
    public async Task return_the_files_for_a_term()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var blue = await AddTermAsync(host, colour.Id, "blue", Token);
        var items = await AddItemsAsync(host, 3, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();
        await store.ReplaceAutoLinksAsync([new ItemTermLinks(items[0], [red.Id]), new ItemTermLinks(items[1], [red.Id, blue.Id]), new ItemTermLinks(items[2], [blue.Id])], Token);

        Ok(await store.GetItemsForTermAsync(red.Id, Token)).ShouldBe(items[..2], ignoreOrder: true);
    }

    [Fact]
    public async Task remove_links_when_the_item_is_removed()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var items = await AddItemsAsync(host, 1, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();
        await store.ReplaceAutoLinksAsync([new ItemTermLinks(items[0], [red.Id])], Token);
        await using (var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token))
            await context.Items.Where(item => item.Id == items[0]).ExecuteDeleteAsync(Token);

        Ok(await store.GetItemsForTermAsync(red.Id, Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task refuse_a_user_link_to_an_item_or_term_that_is_not_stored()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var items = await AddItemsAsync(host, 1, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();
        var missingItem = new ItemId(Guid.NewGuid());
        var missingTerm = new TermId(Guid.NewGuid());

        Error(await store.AssignUserLinkAsync(missingItem, red.Id, Token)).Value.ShouldBe(new ItemNotFound(missingItem));
        Error(await store.AssignUserLinkAsync(items[0], missingTerm, Token)).Value.ShouldBe(new TermNotFound(missingTerm));
    }

    [Fact]
    public async Task assign_a_user_link_twice_without_a_duplicate()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var items = await AddItemsAsync(host, 1, Token);
        var store = host.Services.GetRequiredService<IFileClassificationStore>();

        await store.AssignUserLinkAsync(items[0], red.Id, Token);
        await store.AssignUserLinkAsync(items[0], red.Id, Token);

        Ok(await store.GetTermsForItemAsync(items[0], Token)).ShouldBe([red.Id]);
    }

    [Fact]
    public async Task return_nothing_for_an_item_with_no_links()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        Ok(await host.Services.GetRequiredService<IFileClassificationStore>().GetTermsForItemAsync(new ItemId(Guid.NewGuid()), Token)).ShouldBeEmpty();
    }
}
