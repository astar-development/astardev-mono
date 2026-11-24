using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IClassifiableFileSource"/>. Pages are ordered by item identity and each call uses its own short-lived context.</summary>
internal sealed class ClassifiableFileSource(IDbContextFactory<CloudSyncDbContext> contextFactory) : IClassifiableFileSource
{
    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<ClassifiableFile>, ClassificationError>> GetPageAsync(ItemId? after, int pageSize, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<IReadOnlyList<ClassifiableFile>>(contextFactory, async (context, token) =>
        {
            var lastText = after is { } last ? BulkSql.Text(last.Value) : string.Empty;
            var page = await context.Items.FromSql($"SELECT * FROM Items WHERE ItemType = 'File' AND Id > {lastText} ORDER BY Id LIMIT {pageSize}").AsNoTracking().ToListAsync(token).ConfigureAwait(false);

            return new Ok<IReadOnlyList<ClassifiableFile>>([.. page.Select(item => new ClassifiableFile(item.Id, item.SyncJobId, item.RemoteItemId, item.LocalPath))]);
        }, null, cancellationToken);
}
