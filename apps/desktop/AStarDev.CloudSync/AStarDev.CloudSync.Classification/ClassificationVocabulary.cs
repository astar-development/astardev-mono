using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

internal sealed class ClassificationVocabulary
{
    private ClassificationVocabulary(IReadOnlyList<Term> terms, Dictionary<CategoryId, Category> categories, PhraseIndex phraseIndex, Dictionary<string, Term[]> byExtension)
    {
        Terms = terms;
        CategoriesById = categories;
        PhraseIndex = phraseIndex;
        ByExtension = byExtension;
    }

    internal IReadOnlyList<Term> Terms { get; }

    internal IReadOnlyDictionary<CategoryId, Category> CategoriesById { get; }

    internal PhraseIndex PhraseIndex { get; }

    internal IReadOnlyDictionary<string, Term[]> ByExtension { get; }

    internal static ClassificationVocabulary Create(IReadOnlyList<Category> categories, IReadOnlyList<Term> terms)
    {
        IReadOnlyList<Term> active = [.. terms.Where(term => term.Status != TermStatus.Ignored)];
        var byExtension = active
            .GroupBy(term => term.Text.TrimStart('.'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        return new(active, categories.ToDictionary(category => category.Id), PhraseIndex.Create(active), byExtension);
    }
}
