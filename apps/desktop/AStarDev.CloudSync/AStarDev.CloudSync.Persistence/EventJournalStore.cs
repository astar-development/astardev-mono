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
            var rows = await Matching(context, fromUtc, toUtc, eventTypes).OrderBy(row => row.OccurredUtc).ThenBy(row => row.Id).ToListAsync(token).ConfigureAwait(false);

            return [.. rows.Select(row => row.ToEntry())];
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Page<JournalEntry>, StoreError>> PageAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, IReadOnlyList<string> eventTypes, int skip, int take, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Page<JournalEntry>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var matching = Matching(context, fromUtc, toUtc, eventTypes);
            var total = await matching.CountAsync(token).ConfigureAwait(false);
            var rows = await matching.OrderByDescending(row => row.OccurredUtc).ThenByDescending(row => row.Id).Skip(skip).Take(take).ToListAsync(token).ConfigureAwait(false);

            return new Page<JournalEntry>(total, [.. rows.Select(row => row.ToEntry())]);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<int, StoreError>> PruneAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken)
    {
        var cutoff = olderThanUtc.ToUniversalTime();
        var outcome = await Try.RunAsync<int>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);

            return await context.EventJournal.Where(row => row.OccurredUtc < cutoff).ExecuteDeleteAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <summary>The entries that occurred in <c>[fromUtc, toUtc]</c> and, unless <paramref name="eventTypes"/> is empty, have one of its types, filtered in SQL. Both bounds become UTC first, because the stored binary form of a <see cref="DateTimeOffset"/> orders by instant only at a zero offset; the type filter is left out rather than made optional in SQL, so an <c>(EventType, OccurredUtc)</c> index can serve it.</summary>
    internal static IQueryable<JournalEntryEntity> Matching(CloudSyncDbContext context, DateTimeOffset fromUtc, DateTimeOffset toUtc, IReadOnlyList<string> eventTypes)
    {
        var from = fromUtc.ToUniversalTime();
        var to = toUtc.ToUniversalTime();
        var inRange = context.EventJournal.AsNoTracking().Where(row => row.OccurredUtc >= from && row.OccurredUtc <= to);

        return eventTypes.Count == 0 ? inRange : inRange.Where(row => eventTypes.Contains(row.EventType));
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}
