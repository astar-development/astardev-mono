using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IClassificationStateStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class ClassificationStateStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IClassificationStateStore
{
    private const string SeedVersionName = "SeedVersion";

    /// <inheritdoc/>
    public Task<Result<Option<string>, ClassificationError>> GetSeedVersionAsync(CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Option<string>>(contextFactory, async (context, token) =>
        {
            var entity = await context.ClassificationState.AsNoTracking().SingleOrDefaultAsync(row => row.Name == SeedVersionName, token).ConfigureAwait(false);

            return new Ok<Option<string>>(Option.FromNullable(entity?.Value));
        }, null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> SetSeedVersionAsync(string version, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            var entity = await context.ClassificationState.SingleOrDefaultAsync(row => row.Name == SeedVersionName, token).ConfigureAwait(false);
            if (entity is null) context.ClassificationState.Add(new ClassificationStateEntity { Name = SeedVersionName, Value = version });
            else entity.Value = version;

            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, null, cancellationToken);
}
