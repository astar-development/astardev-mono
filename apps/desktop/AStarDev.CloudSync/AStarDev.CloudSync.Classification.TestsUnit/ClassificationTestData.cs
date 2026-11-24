using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification.TestsUnit;

internal static class ClassificationTestData
{
    internal static readonly CategoryId Colour = new(Guid.NewGuid());

    internal static readonly CategoryId Place = new(Guid.NewGuid());

    internal static RelativePath PathOf(string value) => new(value);

    internal static Term Term(string text, CategoryId category) => Term(text, category, TermSource.Seed, TermStatus.Active);

    internal static Term Term(string text, CategoryId category, TermSource source, TermStatus status) => new(new TermId(Guid.NewGuid()), text, category, source, status);

    internal static ClassificationVocabulary Vocabulary(params Term[] terms) => ClassificationVocabulary.Create([new Category(Colour, "Colour"), new Category(Place, "Place")], terms);

    internal static string[] Texts(IEnumerable<ClassificationCandidate> candidates) => [.. candidates.Select(candidate => candidate.Term.Text)];
}
