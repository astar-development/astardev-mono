using System.Runtime.CompilerServices;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="ITermStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class TermStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : ITermStore
{
    private const int BatchSize = 1000;
    private const int ColumnCount = 5;

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
    public async Task<Result<Unit, ClassificationError>> AddMissingAsync(IReadOnlyList<Term> terms, CancellationToken cancellationToken)
    {
        foreach (var batch in terms.Chunk(BatchSize))
        {
            var result = await ClassificationStore.RunAsync<Unit>(contextFactory, (context, token) => AddMissingBatchAsync(context, batch, token), null, cancellationToken).ConfigureAwait(false);
            if (result.Value is Fail<ClassificationError>) return result;
        }

        return new Ok<Unit>(default);
    }

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

    private static async Task<Result<Unit, ClassificationError>> AddMissingBatchAsync(CloudSyncDbContext context, Term[] batch, CancellationToken cancellationToken)
    {
        var categoryIds = batch.Select(term => term.CategoryId).Distinct().ToList();
        var stored = (await context.Categories.AsNoTracking().Where(category => categoryIds.Contains(category.Id)).Select(category => category.Id).ToListAsync(cancellationToken).ConfigureAwait(false)).ToHashSet();
        var missingCategory = categoryIds.FirstOrDefault(id => !stored.Contains(id));
        if (categoryIds.Any(id => !stored.Contains(id))) return new Fail<ClassificationError>(new CategoryNotFound(missingCategory));

        var distinct = batch.DistinctBy(term => (term.CategoryId, term.Text)).ToArray();
        var format = "INSERT OR IGNORE INTO Terms (Id, Text, CategoryId, Source, Status) VALUES " + BulkSql.Rows(distinct.Length, ColumnCount);
        object[] values = [.. distinct.SelectMany(term => new object[] { BulkSql.Text(term.Id.Value), term.Text, BulkSql.Text(term.CategoryId.Value), term.Source.ToString(), term.Status.ToString() })];
        await context.Database.ExecuteSqlAsync(FormattableStringFactory.Create(format, values), cancellationToken).ConfigureAwait(false);

        return new Ok<Unit>(default);
    }

    private static ClassificationError ToError(Exception exception, Term term)
        => SqliteErrors.IsConstraintViolation(exception) ? new TermAlreadyExists(term.Text, term.CategoryId) : new StorageFailed(exception.Message);
}
