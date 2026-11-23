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

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<JournalEntry>, StoreError>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, IReadOnlyList<string> eventTypes, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<JournalEntry>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var rows = await context.EventJournal.AsNoTracking().ToListAsync(token).ConfigureAwait(false);

            return rows
                .Where(row => row.OccurredUtc >= fromUtc && row.OccurredUtc <= toUtc && (eventTypes.Count == 0 || eventTypes.Contains(row.EventType)))
                .OrderBy(row => row.OccurredUtc)
                .Select(row => row.ToEntry())
                .ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<int, StoreError>> PruneAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<int>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var expired = (await context.EventJournal.ToListAsync(token).ConfigureAwait(false)).Where(row => row.OccurredUtc < olderThanUtc).ToList();
            context.EventJournal.RemoveRange(expired);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return expired.Count;
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
