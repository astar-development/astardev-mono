using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

/// <summary>Classifies a path by running every signal over a vocabulary.</summary>
/// <param name="signals">The signals to run, in any order.</param>
internal sealed class FileClassifier(IEnumerable<IClassificationSignal> signals)
{
    /// <summary>Finds the candidates for <paramref name="path"/>. An empty vocabulary classifies nothing.</summary>
    /// <param name="path">The path below the sync root.</param>
    /// <param name="vocabulary">The vocabulary to match against.</param>
    /// <returns>The merged result; no candidates means unclassified.</returns>
    internal FileClassificationResult Classify(RelativePath path, ClassificationVocabulary vocabulary) =>
        ClassificationCombiner.Combine(path, signals.SelectMany(signal => signal.Evaluate(path, vocabulary)));
}
