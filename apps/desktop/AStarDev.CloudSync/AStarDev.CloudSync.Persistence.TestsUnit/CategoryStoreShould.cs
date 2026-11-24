using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Persistence.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class CategoryStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_added_categories_ordered_by_name_ignoring_case()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var place = await AddCategoryAsync(host, "place", Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);

        var listed = await host.Services.GetRequiredService<ICategoryStore>().ListAsync(Token);

        Ok(listed).ShouldBe([colour, place]);
    }

    [Fact]
    public async Task refuse_a_name_that_is_taken_ignoring_case()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        await AddCategoryAsync(host, "Colour", Token);

        var result = await host.Services.GetRequiredService<ICategoryStore>().AddAsync(new Category(new CategoryId(Guid.NewGuid()), "COLOUR"), Token);

        Error(result).Value.ShouldBe(new CategoryAlreadyExists("COLOUR"));
    }

    [Fact]
    public async Task rename_a_category_and_keep_its_identity()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var store = host.Services.GetRequiredService<ICategoryStore>();

        await store.RenameAsync(colour.Id, "Colours", Token);

        Ok(await store.ListAsync(Token)).ShouldBe([colour with { Name = "Colours" }]);
    }

    [Fact]
    public async Task refuse_to_rename_to_a_name_another_category_has()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        await AddCategoryAsync(host, "Colour", Token);
        var place = await AddCategoryAsync(host, "Place", Token);

        var result = await host.Services.GetRequiredService<ICategoryStore>().RenameAsync(place.Id, "colour", Token);

        Error(result).Value.ShouldBe(new CategoryAlreadyExists("colour"));
    }

    [Fact]
    public async Task report_a_category_that_is_not_stored_when_renaming()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var id = new CategoryId(Guid.NewGuid());

        var result = await host.Services.GetRequiredService<ICategoryStore>().RenameAsync(id, "x", Token);

        Error(result).Value.ShouldBe(new CategoryNotFound(id));
    }

    [Fact]
    public async Task remove_an_empty_category()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        var store = host.Services.GetRequiredService<ICategoryStore>();

        await store.RemoveAsync(colour.Id, Token);

        Ok(await store.ListAsync(Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task refuse_to_remove_a_category_that_holds_terms()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);
        await AddTermAsync(host, colour.Id, "red", Token);

        var result = await host.Services.GetRequiredService<ICategoryStore>().RemoveAsync(colour.Id, Token);

        Error(result).Value.ShouldBe(new CategoryNotEmpty(colour.Id));
    }

    [Fact]
    public async Task store_a_category_that_does_not_exist_when_getting_or_adding()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = new Category(new CategoryId(Guid.NewGuid()), "Colour");

        var result = await host.Services.GetRequiredService<ICategoryStore>().GetOrAddAsync(colour, Token);

        Ok(result).ShouldBe(colour);
    }

    [Fact]
    public async Task return_the_existing_category_when_getting_or_adding_a_name_ignoring_case()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var colour = await AddCategoryAsync(host, "Colour", Token);

        var result = await host.Services.GetRequiredService<ICategoryStore>().GetOrAddAsync(new Category(new CategoryId(Guid.NewGuid()), "COLOUR"), Token);

        Ok(result).ShouldBe(colour);
    }
}
