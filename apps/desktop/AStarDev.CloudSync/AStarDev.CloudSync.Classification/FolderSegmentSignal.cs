using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

/// <summary>Matches the words in each folder name of the path against vocabulary terms; the file name is ignored.</summary>
internal sealed class FolderSegmentSignal : IClassificationSignal
{
    /// <inheritdoc />
    public IReadOnlyList<ClassificationCandidate> Evaluate(RelativePath path, ClassificationVocabulary vocabulary)
    {
        var value = path.Value.AsSpan();
        var folders = value[..Math.Max(value.LastIndexOf('/'), 0)];
        var matches = new List<Term>();
        foreach (var folder in folders.Split('/'))
            PhraseMatcher.MatchInto(matches, ClassificationTokeniser.Tokenise(folders[folder]), vocabulary.PhraseIndex);

        return SignalCandidates.Create(matches, vocabulary, SignalKind.FolderName);
    }
}
