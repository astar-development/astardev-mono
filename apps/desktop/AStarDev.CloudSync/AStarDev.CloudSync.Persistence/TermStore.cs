using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="ITermStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class TermStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : ITermStore
{
    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<Term>, ClassificationError>> ListAsync(CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<IReadOnlyList<Term>>(contextFactory, async (context, token) =>
        {
            var entities = await context.Terms.AsNoTracking().OrderBy(entity => entity.Text).ThenBy(entity => entity.CategoryId).ToListAsync(token).ConfigureAwait(false);

            return new Ok<IReadOnlyList<Term>>([.. entities.Select(entity => entity.ToTerm())]);
        }, null, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> AddAsync(Term term, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            if (!await context.Categories.AnyAsync(category => category.Id == term.CategoryId, token).ConfigureAwait(false)) return new Fail<ClassificationError>(new CategoryNotFound(term.CategoryId));

            context.Terms.Add(TermEntity.From(term));
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, exception => ToError(exception, term), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> UpdateAsync(Term term, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            var entity = await context.Terms.SingleOrDefaultAsync(row => row.Id == term.Id, token).ConfigureAwait(false);
            if (entity is null) return new Fail<ClassificationError>(new TermNotFound(term.Id));
            if (!await context.Categories.AnyAsync(category => category.Id == term.CategoryId, token).ConfigureAwait(false)) return new Fail<ClassificationError>(new CategoryNotFound(term.CategoryId));

            entity.Apply(term);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, exception => ToError(exception, term), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, ClassificationError>> RemoveAsync(TermId id, CancellationToken cancellationToken)
        => ClassificationStore.RunAsync<Unit>(contextFactory, async (context, token) =>
        {
            var entity = await context.Terms.SingleOrDefaultAsync(row => row.Id == id, token).ConfigureAwait(false);
            if (entity is null) return new Fail<ClassificationError>(new TermNotFound(id));

            context.Terms.Remove(entity);
            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return new Ok<Unit>(default);
        }, null, cancellationToken);

    private static ClassificationError ToError(Exception exception, Term term)
        => SqliteErrors.IsConstraintViolation(exception) ? new TermAlreadyExists(term.Text, term.CategoryId) : new StorageFailed(exception.Message);
}
