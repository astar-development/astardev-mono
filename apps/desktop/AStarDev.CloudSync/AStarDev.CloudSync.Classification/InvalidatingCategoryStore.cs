using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification;

/// <summary>Decorates the category store so every successful write drops the cached vocabulary, whoever made it. The provider is resolved when needed, because it reads through this store and resolving it up front would be a cycle.</summary>
/// <param name="inner">The store that holds the categories.</param>
/// <param name="services">Resolves the vocabulary provider.</param>
internal sealed class InvalidatingCategoryStore(ICategoryStore inner, IServiceProvider services) : ICategoryStore
{
    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Category>, ClassificationError>> ListAsync(CancellationToken cancellationToken) => inner.ListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Result<Unit, ClassificationError>> AddAsync(Category category, CancellationToken cancellationToken) => ChangedAsync(inner.AddAsync(category, cancellationToken));

    /// <inheritdoc />
    public async Task<Result<Category, ClassificationError>> GetOrAddAsync(Category category, CancellationToken cancellationToken)
    {
        var result = await inner.GetOrAddAsync(category, cancellationToken).ConfigureAwait(false);
        if (result.Value is Ok<Category>) services.GetRequiredService<IClassificationVocabularyProvider>().Invalidate();

        return result;
    }

    /// <inheritdoc />
    public Task<Result<Unit, ClassificationError>> RenameAsync(CategoryId id, string name, CancellationToken cancellationToken) => ChangedAsync(inner.RenameAsync(id, name, cancellationToken));

    /// <inheritdoc />
    public Task<Result<Unit, ClassificationError>> RemoveAsync(CategoryId id, CancellationToken cancellationToken) => ChangedAsync(inner.RemoveAsync(id, cancellationToken));

    private async Task<Result<Unit, ClassificationError>> ChangedAsync(Task<Result<Unit, ClassificationError>> write)
    {
        var result = await write.ConfigureAwait(false);
        if (result.Value is Ok<Unit>) services.GetRequiredService<IClassificationVocabularyProvider>().Invalidate();

        return result;
    }
}
