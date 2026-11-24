using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification.TestsUnit;

internal sealed class CountingVocabularyProvider(Result<ClassificationVocabulary, ClassificationError> result, int changesBeforeSettling) : IClassificationVocabularyProvider
{
    private long generation;

    public int Loads { get; private set; }

    public long Generation => generation;

    public Task<Result<ClassificationVocabulary, ClassificationError>> GetAsync(CancellationToken cancellationToken)
    {
        Loads++;
        if (Loads <= changesBeforeSettling) generation++;

        return Task.FromResult(result);
    }

    public void Invalidate() => generation++;
}
