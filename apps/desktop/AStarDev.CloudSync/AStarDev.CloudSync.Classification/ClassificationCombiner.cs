using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

/// <summary>Merges candidates from several signals into one result.</summary>
internal static class ClassificationCombiner
{
    /// <summary>Drops ignored terms, de-duplicates by term text and category with High confidence beating Low, and returns the result. No candidates means unclassified.</summary>
    /// <param name="path">The classified file path.</param>
    /// <param name="candidates">Candidates from every signal.</param>
    /// <returns>The merged result.</returns>
    internal static FileClassificationResult Combine(RelativePath path, IEnumerable<ClassificationCandidate> candidates) =>
        new(path, [.. candidates
            .Where(candidate => candidate.Term.Status != TermStatus.Ignored)
            .GroupBy(candidate => (candidate.Term.Text, candidate.Term.CategoryId))
            .Select(group => group.OrderBy(candidate => candidate.Confidence).First())]);
}
