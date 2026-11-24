using System.Runtime.CompilerServices;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IFileClassificationStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class FileClassificationStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IFileClassificationStore
{
    private const int BatchSize = 1000;
    private const int StatementRows = 1000;

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
    public async Task<Result<Unit, ClassificationError>> ReplaceAutoLinksAsync(SyncJobId syncJobId, IReadOnlyList<RemoteItemTermLinks> updates, CancellationToken cancellationToken)
    {
        foreach (var batch in updates.Chunk(BatchSize))
        {
            var result = await ClassificationStore.RunAsync<Unit>(contextFactory, (context, token) => ReplaceRemoteBatchAsync(context, syncJobId, batch, token), null, cancellationToken).ConfigureAwait(false);
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
    public Task<Result<TermFilesPage, ClassificationError>> GetFilesForTermAsync(TermId termId, int skip, int take, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<TermFilesPage>(contextFactory, async (context, token) =>
        {
            var files = from link in context.FileLinks.AsNoTracking()
                        where link.TermId == termId
                        join item in context.Items on link.ItemId equals item.Id
                        join job in context.SyncJobs on item.SyncJobId equals job.Id
                        join account in context.Accounts on job.AccountId equals account.Id
                        select new { item.Id, item.LocalPath, account.DisplayName };
            var total = await files.CountAsync(token).ConfigureAwait(false);
            var page = await files.OrderBy(file => file.LocalPath).ThenBy(file => file.Id).Skip(skip).Take(take).ToListAsync(token).ConfigureAwait(false);

            return new Ok<TermFilesPage>(new TermFilesPage(total, [.. page.Select(file => new TermFile(file.Id, file.LocalPath, file.DisplayName))]));
        }, null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyDictionary<TermId, int>, ClassificationError>> GetFileCountsAsync(CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<IReadOnlyDictionary<TermId, int>>(contextFactory, async (context, token) =>
        {
            var counts = await context.FileLinks.AsNoTracking().GroupBy(link => link.TermId).Select(group => new { TermId = group.Key, Count = group.Count() }).ToListAsync(token).ConfigureAwait(false);

            return new Ok<IReadOnlyDictionary<TermId, int>>(counts.ToDictionary(row => row.TermId, row => row.Count));
        }, null, cancellationToken);

    private static async Task<Result<Unit, ClassificationError>> ReplaceRemoteBatchAsync(CloudSyncDbContext context, SyncJobId syncJobId, RemoteItemTermLinks[] batch, CancellationToken cancellationToken)
    {
        var remoteIds = batch.Select(update => update.RemoteItemId).Distinct().ToList();
        var resolved = (await context.Items.AsNoTracking().Where(item => item.SyncJobId == syncJobId && remoteIds.Contains(item.RemoteItemId)).Select(item => new { item.RemoteItemId, item.Id }).ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(item => item.RemoteItemId)
            .ToDictionary(group => group.Key, group => group.First().Id);

        return await ReplaceBatchAsync(context, [.. batch.Where(update => resolved.ContainsKey(update.RemoteItemId)).Select(update => new ItemTermLinks(resolved[update.RemoteItemId], update.TermIds))], cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Unit, ClassificationError>> ReplaceBatchAsync(CloudSyncDbContext context, ItemTermLinks[] batch, CancellationToken cancellationToken)
    {
        var itemIds = batch.Select(update => update.ItemId).Distinct().ToList();
        var stored = (await context.Items.AsNoTracking().Where(item => itemIds.Contains(item.Id)).Select(item => item.Id).ToListAsync(cancellationToken).ConfigureAwait(false)).ToHashSet();
        var existing = (await context.FileLinks.AsNoTracking().Where(link => itemIds.Contains(link.ItemId)).ToListAsync(cancellationToken).ConfigureAwait(false)).ToLookup(link => link.ItemId);
        List<(ItemId ItemId, TermId TermId)> remove = [];
        List<(ItemId ItemId, TermId TermId)> add = [];
        foreach (var update in batch.Where(update => stored.Contains(update.ItemId)))
        {
            var current = existing[update.ItemId].ToList();
            var wanted = update.TermIds.ToHashSet();
            var held = current.Select(link => link.TermId).ToHashSet();
            remove.AddRange(current.Where(link => link.Origin == FileLinkOrigin.Auto && !wanted.Contains(link.TermId)).Select(link => (link.ItemId, link.TermId)));
            add.AddRange(wanted.Where(termId => !held.Contains(termId)).Select(termId => (update.ItemId, termId)));
        }

        if (remove.Count == 0 && add.Count == 0) return new Ok<Unit>(default);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rows in remove.Chunk(StatementRows))
            await context.Database.ExecuteSqlAsync(FormattableStringFactory.Create("DELETE FROM FileLinks WHERE Origin = 'Auto' AND (ItemId, TermId) IN (VALUES " + BulkSql.Rows(rows.Length, 2) + ")", rows.SelectMany(row => new object[] { BulkSql.Text(row.ItemId.Value), BulkSql.Text(row.TermId.Value) }).ToArray()), cancellationToken).ConfigureAwait(false);

        foreach (var rows in add.Chunk(StatementRows))
            await context.Database.ExecuteSqlAsync(FormattableStringFactory.Create("INSERT OR IGNORE INTO FileLinks (ItemId, TermId, Origin) VALUES " + BulkSql.Rows(rows.Length, 3), rows.SelectMany(row => new object[] { BulkSql.Text(row.ItemId.Value), BulkSql.Text(row.TermId.Value), nameof(FileLinkOrigin.Auto) }).ToArray()), cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new Ok<Unit>(default);
    }
}
