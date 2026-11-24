using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

/// <summary>Matches the file extension against vocabulary terms such as "jpg" or ".jpg". The terms are indexed by extension once per vocabulary, so a lookup does not scan every term.</summary>
internal sealed class ExtensionSignal : IClassificationSignal
{
    /// <inheritdoc />
    public IReadOnlyList<ClassificationCandidate> Evaluate(RelativePath path, ClassificationVocabulary vocabulary)
    {
        var extension = Path.GetExtension(path.Value).TrimStart('.');

        return extension.Length == 0 || !vocabulary.ByExtension.TryGetValue(extension, out var terms)
            ? []
            : SignalCandidates.Create(terms, vocabulary, SignalKind.Extension);
    }
}
