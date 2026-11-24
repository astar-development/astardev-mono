using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IFileClassificationStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class FileClassificationStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IFileClassificationStore
{
    private const int BatchSize = 1000;

    /// <inheritdoc/>
    public async Task<Result<Unit, ClassificationError>> ReplaceAutoLinksAsync(IReadOnlyList<ItemTermLinks> updates, CancellationToken cancellationToken)
    {
        foreach (var batch in updates.Chunk(BatchSize))
        {
            var result = await ClassificationStore.RunAsync<Unit>(contextFactory, (context, token) => ReplaceBatchAsync(context, batch, token), null, cancellationToken).ConfigureAwait(false);
            if (result.Value is Fail<ClassificationError>) return result;
        }

        return new Ok<Unit>(default);
    }

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> AssignUserLinkAsync(ItemId itemId, TermId termId, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            if (!await context.Items.AnyAsync(item => item.Id == itemId, token).ConfigureAwait(false)) return new Fail<ClassificationError>(new ItemNotFound(itemId));
            if (!await context.Terms.AnyAsync(term => term.Id == termId, token).ConfigureAwait(false)) return new Fail<ClassificationError>(new TermNotFound(termId));

            var link = await context.FileLinks.SingleOrDefaultAsync(row => row.ItemId == itemId && row.TermId == termId, token).ConfigureAwait(false);
            if (link is null) context.FileLinks.Add(new FileLinkEntity { ItemId = itemId, TermId = termId, Origin = FileLinkOrigin.User });
            else link.Origin = FileLinkOrigin.User;

            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<TermId>, ClassificationError>> GetTermsForItemAsync(ItemId itemId, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<IReadOnlyList<TermId>>(contextFactory, async (context, token) =>
            new Ok<IReadOnlyList<TermId>>(await context.FileLinks.AsNoTracking().Where(link => link.ItemId == itemId).Select(link => link.TermId).ToListAsync(token).ConfigureAwait(false)), null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<ItemId>, ClassificationError>> GetItemsForTermAsync(TermId termId, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<IReadOnlyList<ItemId>>(contextFactory, async (context, token) =>
            new Ok<IReadOnlyList<ItemId>>(await context.FileLinks.AsNoTracking().Where(link => link.TermId == termId).Select(link => link.ItemId).ToListAsync(token).ConfigureAwait(false)), null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyDictionary<TermId, int>, ClassificationError>> GetFileCountsAsync(CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<IReadOnlyDictionary<TermId, int>>(contextFactory, async (context, token) =>
        {
            var counts = await context.FileLinks.AsNoTracking().GroupBy(link => link.TermId).Select(group => new { TermId = group.Key, Count = group.Count() }).ToListAsync(token).ConfigureAwait(false);

            return new Ok<IReadOnlyDictionary<TermId, int>>(counts.ToDictionary(row => row.TermId, row => row.Count));
        }, null, cancellationToken);

    private static async Task<Result<Unit, ClassificationError>> ReplaceBatchAsync(CloudSyncDbContext context, ItemTermLinks[] batch, CancellationToken cancellationToken)
    {
        var itemIds = batch.Select(update => update.ItemId).Distinct().ToList();
        var stored = (await context.Items.AsNoTracking().Where(item => itemIds.Contains(item.Id)).Select(item => item.Id).ToListAsync(cancellationToken).ConfigureAwait(false)).ToHashSet();
        var existing = (await context.FileLinks.Where(link => itemIds.Contains(link.ItemId)).ToListAsync(cancellationToken).ConfigureAwait(false)).ToLookup(link => link.ItemId);

        foreach (var update in batch.Where(update => stored.Contains(update.ItemId)))
            Replace(context, update, existing[update.ItemId].ToList());

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new Ok<Unit>(default);
    }

    private static void Replace(CloudSyncDbContext context, ItemTermLinks update, List<FileLinkEntity> current)
    {
        var wanted = update.TermIds.ToHashSet();
        context.FileLinks.RemoveRange(current.Where(link => link.Origin == FileLinkOrigin.Auto && !wanted.Contains(link.TermId)));
        var held = current.Select(link => link.TermId).ToHashSet();
        context.FileLinks.AddRange(wanted.Where(termId => !held.Contains(termId)).Select(termId => new FileLinkEntity { ItemId = update.ItemId, TermId = termId, Origin = FileLinkOrigin.Auto }));
    }
}
