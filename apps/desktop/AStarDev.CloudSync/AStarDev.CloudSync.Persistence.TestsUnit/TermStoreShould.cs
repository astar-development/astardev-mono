using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Persistence.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class TermStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_added_terms_ordered_by_text()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var blue = await AddTermAsync(host, colour.Id, "blue", Token);

        var listed = await host.Services.GetRequiredService<ITermStore>().ListAsync(Token);

        Ok(listed).ShouldBe([blue, red]);
    }

    [Fact]
    public async Task refuse_a_term_for_a_category_that_is_not_stored()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var term = new Term(new TermId(Guid.NewGuid()), "red", new CategoryId(Guid.NewGuid()), TermSource.User, TermStatus.Active);

        var result = await host.Services.GetRequiredService<ITermStore>().AddAsync(term, Token);

        Error(result).Value.ShouldBe(new CategoryNotFound(term.CategoryId));
    }

    [Fact]
    public async Task refuse_a_text_the_category_already_holds()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        await AddTermAsync(host, colour.Id, "red", Token);

        var result = await host.Services.GetRequiredService<ITermStore>().AddAsync(new Term(new TermId(Guid.NewGuid()), "red", colour.Id, TermSource.User, TermStatus.Active), Token);

        Error(result).Value.ShouldBe(new TermAlreadyExists("red", colour.Id));
    }

    [Fact]
    public async Task allow_the_same_text_in_two_categories()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var place = await AddCategoryAsync(host, "Place", Token);
        await AddTermAsync(host, colour.Id, "china", Token);

        var result = await host.Services.GetRequiredService<ITermStore>().AddAsync(new Term(new TermId(Guid.NewGuid()), "china", place.Id, TermSource.Seed, TermStatus.Active), Token);

        Ok(result).ShouldBe(default);
    }

    [Fact]
    public async Task update_a_term_and_keep_its_identity_and_links()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var items = await AddItemsAsync(host, 1, Token);
        await host.Services.GetRequiredService<IFileClassificationStore>().AssignUserLinkAsync(items[0], red.Id, Token);
        var store = host.Services.GetRequiredService<ITermStore>();

        await store.UpdateAsync(red with { Status = TermStatus.Ignored }, Token);

        Ok(await store.ListAsync(Token)).ShouldBe([red with { Status = TermStatus.Ignored }]);
        Ok(await host.Services.GetRequiredService<IFileClassificationStore>().GetTermsForItemAsync(items[0], Token)).ShouldBe([red.Id]);
    }

    [Fact]
    public async Task report_a_term_that_is_not_stored_when_updating()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var term = new Term(new TermId(Guid.NewGuid()), "red", colour.Id, TermSource.User, TermStatus.Active);

        var result = await host.Services.GetRequiredService<ITermStore>().UpdateAsync(term, Token);

        Error(result).Value.ShouldBe(new TermNotFound(term.Id));
    }

    [Fact]
    public async Task remove_a_term_and_its_links()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var red = await AddTermAsync(host, colour.Id, "red", Token);
        var items = await AddItemsAsync(host, 1, Token);
        var links = host.Services.GetRequiredService<IFileClassificationStore>();
        await links.AssignUserLinkAsync(items[0], red.Id, Token);

        await host.Services.GetRequiredService<ITermStore>().RemoveAsync(red.Id, Token);

        Ok(await links.GetTermsForItemAsync(items[0], Token)).ShouldBeEmpty();
    }
}
