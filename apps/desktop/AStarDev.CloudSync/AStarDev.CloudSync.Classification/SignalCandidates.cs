using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

internal static class SignalCandidates
{
    internal static IReadOnlyList<ClassificationCandidate> Create(IEnumerable<Term> terms, ClassificationVocabulary vocabulary, SignalKind signal) =>
        [.. terms
            .Where(term => vocabulary.CategoriesById.ContainsKey(term.CategoryId))
            .Select(term => new ClassificationCandidate(term, vocabulary.CategoriesById[term.CategoryId], signal, ConfidenceOf(term)))];

    private static ClassificationConfidence ConfidenceOf(Term term) => term.Source == TermSource.Suggested ? ClassificationConfidence.Low : ClassificationConfidence.High;
}
