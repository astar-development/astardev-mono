using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Loads the categories and terms once and keeps the vocabulary until <see cref="Invalidate"/>. Concurrent callers share one load; a load running when <see cref="Invalidate"/> is called, a failed load and a cancelled load are not kept.</summary>
/// <param name="terms">Stores terms.</param>
/// <param name="categories">Stores categories.</param>
internal sealed class ClassificationVocabularyProvider(ITermStore terms, ICategoryStore categories) : IClassificationVocabularyProvider
{
    private readonly Lock gate = new();
    private long generation;
    private Task<Result<ClassificationVocabulary, ClassificationError>>? current;

    /// <inheritdoc />
    public long Generation => Interlocked.Read(ref generation);

    /// <inheritdoc />
    public async Task<Result<ClassificationVocabulary, ClassificationError>> GetAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (load, startedAt) = JoinOrStart(cancellationToken);
            await ((Task)load).WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            cancellationToken.ThrowIfCancellationRequested();
            if (load.IsCanceled) continue;

            var result = await load.ConfigureAwait(false);
            if (result.Value is Fail<ClassificationError> || Generation != startedAt) Forget(load);

            return result;
        }
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        lock (gate)
        {
            Interlocked.Increment(ref generation);
            current = null;
        }
    }

    private (Task<Result<ClassificationVocabulary, ClassificationError>> Load, long StartedAt) JoinOrStart(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            current ??= ReadAsync(cancellationToken);

            return (current, Generation);
        }
    }

    private void Forget(Task<Result<ClassificationVocabulary, ClassificationError>> load)
    {
        lock (gate)
        {
            if (ReferenceEquals(current, load)) current = null;
        }
    }

    private async Task<Result<ClassificationVocabulary, ClassificationError>> ReadAsync(CancellationToken cancellationToken)
    {
        var categoryResult = await categories.ListAsync(cancellationToken).ConfigureAwait(false);
        var termResult = await terms.ListAsync(cancellationToken).ConfigureAwait(false);

        return categoryResult switch
        {
            Ok<IReadOnlyList<Category>> loadedCategories => termResult switch
            {
                Ok<IReadOnlyList<Term>> loadedTerms => new Ok<ClassificationVocabulary>(ClassificationVocabulary.Create(loadedCategories.Value, loadedTerms.Value)),
                Fail<ClassificationError> failure => failure
            },
            Fail<ClassificationError> failure => failure
        };
    }
}
