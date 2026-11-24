using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Supplies the current vocabulary, loaded once and kept until it is invalidated.</summary>
internal interface IClassificationVocabularyProvider
{
    /// <summary>Counts the invalidations. A caller that classified a batch compares it before and after to learn whether the vocabulary changed meanwhile.</summary>
    long Generation { get; }

    /// <summary>Returns the cached vocabulary, loading it from the stores when none is cached. Callers that arrive during a load share it.</summary>
    /// <param name="cancellationToken">Stops the wait; a load started by another caller carries on for the others.</param>
    /// <returns>The vocabulary, or the store's failure. A failed load is not kept, so the next call retries.</returns>
    Task<Result<ClassificationVocabulary, ClassificationError>> GetAsync(CancellationToken cancellationToken);

    /// <summary>Drops the cached vocabulary and bumps <see cref="Generation"/>. A load running at that moment is not kept.</summary>
    void Invalidate();
}
