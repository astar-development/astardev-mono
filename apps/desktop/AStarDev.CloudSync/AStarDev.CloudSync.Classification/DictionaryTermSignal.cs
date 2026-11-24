using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

/// <summary>Matches the words in the file name, without its extension, against vocabulary terms.</summary>
internal sealed class DictionaryTermSignal : IClassificationSignal
{
    /// <inheritdoc />
    public IReadOnlyList<ClassificationCandidate> Evaluate(RelativePath path, ClassificationVocabulary vocabulary)
    {
        var matches = new List<Term>();
        PhraseMatcher.MatchInto(matches, ClassificationTokeniser.Tokenise(Path.GetFileNameWithoutExtension(path.Value.AsSpan())), vocabulary.PhraseIndex);

        return SignalCandidates.Create(matches, vocabulary, SignalKind.Vocabulary);
    }
}
