using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Persistence.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class ClassifiableFileSourceShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_every_file_a_page_at_a_time()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var items = await AddItemsAsync(host, 5, Token);
        var source = host.Services.GetRequiredService<IClassifiableFileSource>();
        var seen = new List<ItemId>();
        ItemId? after = null;

        while (true)
        {
            var page = Ok(await source.GetPageAsync(after, 2, Token));
            seen.AddRange(page.Select(file => file.ItemId));
            if (page.Count < 2) break;

            after = page[^1].ItemId;
        }

        seen.ShouldBe(items, ignoreOrder: true);
        seen.Count.ShouldBe(5);
    }

    [Fact]
    public async Task return_the_path_and_remote_id_of_a_file()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var items = await AddItemsAsync(host, 1, Token);

        var page = Ok(await host.Services.GetRequiredService<IClassifiableFileSource>().GetPageAsync(null, 10, Token));

        page.Single().ShouldBe(page.Single() with { ItemId = items[0], Path = new RelativePath("Docs/0.txt"), RemoteItemId = new RemoteItemId("remote-0") });
    }

    [Fact]
    public async Task return_nothing_when_there_are_no_files()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        Ok(await host.Services.GetRequiredService<IClassifiableFileSource>().GetPageAsync(null, 10, Token)).ShouldBeEmpty();
    }
}
