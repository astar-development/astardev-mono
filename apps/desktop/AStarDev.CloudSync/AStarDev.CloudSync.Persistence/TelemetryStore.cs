using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>
/// SQLite implementation of <see cref="ITelemetryStore"/> over <c>telemetry.db</c>. The file and its schema are created by the first call that reaches it;
/// a call that cannot open or migrate the file fails with <see cref="StorageFailed"/> and the next call tries again.
/// </summary>
internal sealed class TelemetryStore(IDbContextFactory<TelemetryDbContext> contextFactory) : ITelemetryStore
{
    private readonly SemaphoreSlim migration = new(1, 1);
    private volatile bool migrated;

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> AppendAsync(IReadOnlyList<TelemetryEntry> entries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (entries.Count == 0) return new Ok<Unit>(default);

        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await OpenAsync(token).ConfigureAwait(false);
            context.Entries.AddRange(entries.Select(ToEntity));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return default(Unit);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<TelemetryEntry>, StoreError>> QueryAsync(TelemetryQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await OpenAsync(token).ConfigureAwait(false);
            var kind = (int)query.Kind;
            var since = query.Since.UtcTicks;
            var pattern = $"%{query.Text}%";
            var rows = await context.Entries.AsNoTracking()
                .Where(entity => entity.Kind == kind && entity.TimestampTicks >= since && entity.Level >= query.MinimumLevel && EF.Functions.Like(entity.Text, pattern))
                .OrderByDescending(entity => entity.TimestampTicks)
                .ThenByDescending(entity => entity.Id)
                .Take(Math.Max(query.Limit, 0))
                .ToListAsync(token).ConfigureAwait(false);

            return (IReadOnlyList<TelemetryEntry>)rows.Select(ToEntry).ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<int, StoreError>> PruneAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await OpenAsync(token).ConfigureAwait(false);
            var cutoff = olderThan.UtcTicks;

            return await context.Entries.Where(entity => entity.TimestampTicks < cutoff).ExecuteDeleteAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static TelemetryEntryEntity ToEntity(TelemetryEntry entry)
        => new() { Kind = (int)entry.Kind, TimestampTicks = entry.Timestamp.UtcTicks, Level = entry.Level, Source = entry.Source, Text = entry.Text };

    private static TelemetryEntry ToEntry(TelemetryEntryEntity entity)
        => new((TelemetryKind)entity.Kind, new DateTimeOffset(entity.TimestampTicks, TimeSpan.Zero), entity.Level, entity.Source, entity.Text);

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);

    private async Task<TelemetryDbContext> OpenAsync(CancellationToken cancellationToken)
    {
        await EnsureMigratedAsync(cancellationToken).ConfigureAwait(false);

        return await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureMigratedAsync(CancellationToken cancellationToken)
    {
        if (migrated) return;

        await migration.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (migrated) return;

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            migrated = true;
        }
        finally
        {
            migration.Release();
        }
    }
}
