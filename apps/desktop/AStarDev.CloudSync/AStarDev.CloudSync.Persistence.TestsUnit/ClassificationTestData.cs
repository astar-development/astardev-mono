using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

internal static class ClassificationTestData
{
    internal static async Task<Category> AddCategoryAsync(PersistenceTestHost host, string name, CancellationToken cancellationToken)
    {
        var category = new Category(new CategoryId(Guid.NewGuid()), name);
        await host.Services.GetRequiredService<ICategoryStore>().AddAsync(category, cancellationToken);

        return category;
    }

    internal static async Task<Term> AddTermAsync(PersistenceTestHost host, CategoryId categoryId, string text, CancellationToken cancellationToken)
    {
        var term = new Term(new TermId(Guid.NewGuid()), text, categoryId, TermSource.Seed, TermStatus.Active);
        await host.Services.GetRequiredService<ITermStore>().AddAsync(term, cancellationToken);

        return term;
    }

    internal static async Task<ItemId[]> AddItemsAsync(PersistenceTestHost host, int count, CancellationToken cancellationToken)
    {
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var now = DateTimeOffset.UtcNow;
        var ids = Enumerable.Range(0, count).Select(_ => new ItemId(Guid.NewGuid())).ToArray();
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(cancellationToken);
        context.Items.AddRange(ids.Select((id, index) => new ItemEntity { Id = id, SyncJobId = job.Id, ItemType = ItemType.File, RemoteItemId = new RemoteItemId($"remote-{index}"), LocalPath = new RelativePath($"Docs/{index}.txt"), CreatedUtc = now, UpdatedUtc = now }));
        await context.SaveChangesAsync(cancellationToken);

        return ids;
    }

    internal static T Ok<T>(Result<T, ClassificationError> result) => result.Value.ShouldBeOfType<Ok<T>>().Value;

    internal static ClassificationError Error<T>(Result<T, ClassificationError> result) => result.Value.ShouldBeOfType<Fail<ClassificationError>>().Error;
}
