using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

/// <summary>One way of finding candidates in a path. A pure function over an in-memory vocabulary.</summary>
internal interface IClassificationSignal
{
    /// <summary>Finds the candidates this signal sees in <paramref name="path"/>. The vocabulary holds no ignored terms.</summary>
    /// <param name="path">The path below the sync root, with <c>/</c> separators.</param>
    /// <param name="vocabulary">The categories and terms to match against.</param>
    IReadOnlyList<ClassificationCandidate> Evaluate(RelativePath path, ClassificationVocabulary vocabulary);
}
