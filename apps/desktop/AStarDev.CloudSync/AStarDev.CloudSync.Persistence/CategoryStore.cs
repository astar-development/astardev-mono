using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="ICategoryStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class CategoryStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : ICategoryStore
{
    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<Category>, ClassificationError>> ListAsync(CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<IReadOnlyList<Category>>(contextFactory, async (context, token) =>
        {
            var entities = await context.Categories.AsNoTracking().OrderBy(entity => entity.Name).ToListAsync(token).ConfigureAwait(false);

            return new Ok<IReadOnlyList<Category>>([.. entities.Select(entity => entity.ToCategory())]);
        }, null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> AddAsync(Category category, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            context.Categories.Add(CategoryEntity.From(category));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, exception => ToError(exception, category.Name), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Category, ClassificationError>> GetOrAddAsync(Category category, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Category>(contextFactory, async (context, token) =>
        {
            var existing = await context.Categories.AsNoTracking().SingleOrDefaultAsync(row => row.Name == category.Name, token).ConfigureAwait(false);
            if (existing is not null) return new Ok<Category>(existing.ToCategory());

            context.Categories.Add(CategoryEntity.From(category));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Category>(category);
        }, null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> RenameAsync(CategoryId id, string name, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            var entity = await context.Categories.SingleOrDefaultAsync(row => row.Id == id, token).ConfigureAwait(false);
            if (entity is null) return new Fail<ClassificationError>(new CategoryNotFound(id));

            entity.Name = name;
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, exception => ToError(exception, name), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> RemoveAsync(CategoryId id, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            var entity = await context.Categories.SingleOrDefaultAsync(row => row.Id == id, token).ConfigureAwait(false);
            if (entity is null) return new Fail<ClassificationError>(new CategoryNotFound(id));
            if (await context.Terms.AnyAsync(term => term.CategoryId == id, token).ConfigureAwait(false)) return new Fail<ClassificationError>(new CategoryNotEmpty(id));

            context.Categories.Remove(entity);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, null, cancellationToken);

    private static ClassificationError ToError(Exception exception, string name)
        => SqliteErrors.IsConstraintViolation(exception) ? new CategoryAlreadyExists(name) : new StorageFailed(exception.Message);
}
