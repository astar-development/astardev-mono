using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification;

/// <summary>Decorates the term store so every successful write drops the cached vocabulary, whoever made it. The provider is resolved when needed, because it reads through this store and resolving it up front would be a cycle.</summary>
/// <param name="inner">The store that holds the terms.</param>
/// <param name="services">Resolves the vocabulary provider.</param>
internal sealed class InvalidatingTermStore(ITermStore inner, IServiceProvider services) : ITermStore
{
    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Term>, ClassificationError>> ListAsync(CancellationToken cancellationToken) => inner.ListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Result<Unit, ClassificationError>> AddAsync(Term term, CancellationToken cancellationToken) => ChangedAsync(inner.AddAsync(term, cancellationToken));

    /// <inheritdoc />
    public Task<Result<Unit, ClassificationError>> AddMissingAsync(IReadOnlyList<Term> terms, CancellationToken cancellationToken) => ChangedAsync(inner.AddMissingAsync(terms, cancellationToken));

    /// <inheritdoc />
    public Task<Result<Unit, ClassificationError>> UpdateAsync(Term term, CancellationToken cancellationToken) => ChangedAsync(inner.UpdateAsync(term, cancellationToken));

    /// <inheritdoc />
    public Task<Result<Unit, ClassificationError>> RemoveAsync(TermId id, CancellationToken cancellationToken) => ChangedAsync(inner.RemoveAsync(id, cancellationToken));

    private async Task<Result<Unit, ClassificationError>> ChangedAsync(Task<Result<Unit, ClassificationError>> write)
    {
        var result = await write.ConfigureAwait(false);
        if (result.Value is Ok<Unit>) services.GetRequiredService<IClassificationVocabularyProvider>().Invalidate();

        return result;
    }
}
