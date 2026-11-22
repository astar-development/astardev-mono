using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IEventJournal"/>. Each call uses its own short-lived context.</summary>
internal sealed class EventJournalStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IEventJournal
{
    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> AppendAsync(JournalEntry entry, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Unit>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            context.EventJournal.Add(JournalEntryEntity.From(entry));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return default;
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<JournalEntry>, StoreError>> ListAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<JournalEntry>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var rows = await context.EventJournal.AsNoTracking().Where(row => row.AggregateId == aggregateId).ToListAsync(token).ConfigureAwait(false);

            return rows.OrderBy(row => row.OccurredUtc).Select(row => row.ToEntry()).ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
