using System.Globalization;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IReclassifyRunStore"/>, kept as three rows of the classification state table. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class ReclassifyRunStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IReclassifyRunStore
{
    private const string CompletedName = "LastRunCompletedUtc";
    private const string FilesName = "LastRunFilesClassified";
    private const string SeedName = "LastRunSeedVersion";

    /// <inheritdoc/>
    public Task<Result<Option<ReclassifyRun>, ClassificationError>> GetLastCompletedAsync(CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Option<ReclassifyRun>>(contextFactory, async (context, token) =>
        {
            var rows = await context.ClassificationState.AsNoTracking().Where(row => row.Name == CompletedName || row.Name == FilesName || row.Name == SeedName).ToDictionaryAsync(row => row.Name, row => row.Value, token).ConfigureAwait(false);
            if (rows.Count == 3 && DateTimeOffset.TryParse(rows[CompletedName], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var completed) && long.TryParse(rows[FilesName], CultureInfo.InvariantCulture, out var files))
                return new Ok<Option<ReclassifyRun>>(Option.Some(new ReclassifyRun(completed, files, rows[SeedName])));

            return new Ok<Option<ReclassifyRun>>(Option.None<ReclassifyRun>());
        }, null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> RecordCompletedAsync(ReclassifyRun run, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            var values = new Dictionary<string, string>
            {
                [CompletedName] = run.CompletedUtc.ToString("O", CultureInfo.InvariantCulture),
                [FilesName] = run.FilesClassified.ToString(CultureInfo.InvariantCulture),
                [SeedName] = run.SeedVersion
            };
            var existing = await context.ClassificationState.Where(row => values.Keys.Contains(row.Name)).ToDictionaryAsync(row => row.Name, token).ConfigureAwait(false);
            foreach (var (name, value) in values)
            {
                if (existing.TryGetValue(name, out var row)) row.Value = value;
                else context.ClassificationState.Add(new ClassificationStateEntity { Name = name, Value = value });
            }

            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, null, cancellationToken);
}
