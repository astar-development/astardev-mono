using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetClassificationTerms"/> from the category, term and link stores. The vocabulary is far larger than one IPC message may be, so the query filters, orders and pages it.</summary>
internal sealed class TermsQuery(ICategoryStore categories, ITermStore terms, IFileClassificationStore links)
{
    private const int MaxTake = 500;

    /// <summary>Reads the categories, the terms and how many files each term has, and returns the requested page.</summary>
    /// <returns>Every category and the requested page of the terms that match the filter, with how many match in all; a negative <see cref="GetClassificationTerms.Skip"/> counts as zero and a <see cref="GetClassificationTerms.Take"/> outside 1 to 500 is brought into that range. Or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ClassificationTermsView, IpcError>> HandleAsync(GetClassificationTerms query, CancellationToken cancellationToken)
    {
        var storedCategories = await categories.ListAsync(cancellationToken);
        var storedTerms = await terms.ListAsync(cancellationToken);
        var counts = await links.GetFileCountsAsync(cancellationToken);

        return (storedCategories, storedTerms, counts) switch
        {
            (Ok<IReadOnlyList<Category>> categoryList, Ok<IReadOnlyList<Term>> termList, Ok<IReadOnlyDictionary<TermId, int>> countList) => new Ok<ClassificationTermsView>(View(query, categoryList.Value, termList.Value, countList.Value)),
            (Fail<ClassificationError> fail, _, _) => Failed(fail.Error),
            (_, Fail<ClassificationError> fail, _) => Failed(fail.Error),
            (_, _, Fail<ClassificationError> fail) => Failed(fail.Error),
            _ => Failed(new StorageFailed("The vocabulary could not be read"))
        };
    }

    private static Fail<IpcError> Failed(ClassificationError error) => new(new HandlerFailed(ClassificationErrorMessages.Describe(error, "read", "classification terms")));

    private static ClassificationTermsView View(GetClassificationTerms query, IReadOnlyList<Category> categories, IReadOnlyList<Term> terms, IReadOnlyDictionary<TermId, int> counts)
    {
        var names = categories.ToDictionary(category => category.Id, category => category.Name);
        var filter = query.Filter.Trim();
        var matching = terms
            .Select(term => (Term: term, Category: names.GetValueOrDefault(term.CategoryId, string.Empty), Files: counts.GetValueOrDefault(term.Id)))
            .Where(entry => filter.Length == 0 || entry.Term.Text.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || entry.Category.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
        var ordered = Order(matching, query.Sort);
        var page = (query.Descending ? ordered.Reverse() : ordered).Skip(Math.Max(query.Skip, 0)).Take(Math.Clamp(query.Take, 1, MaxTake));

        return new ClassificationTermsView(
            [.. categories.Select(category => new ClassificationCategoryView(category.Id.Value, category.Name))],
            [.. page.Select(entry => new ClassificationTermView(entry.Term.Id.Value, entry.Term.Text, entry.Term.CategoryId.Value, entry.Term.Source.ToString(), entry.Term.Status == TermStatus.Ignored, entry.Files))],
            matching.Count);
    }

    private static IEnumerable<(Term Term, string Category, int Files)> Order(List<(Term Term, string Category, int Files)> entries, ClassificationTermSort sort) => sort switch
    {
        ClassificationTermSort.Category => entries.OrderBy(entry => entry.Category, StringComparer.CurrentCultureIgnoreCase).ThenBy(entry => entry.Term.Text, StringComparer.CurrentCultureIgnoreCase),
        ClassificationTermSort.Files => entries.OrderBy(entry => entry.Files).ThenBy(entry => entry.Term.Text, StringComparer.CurrentCultureIgnoreCase),
        _ => entries.OrderBy(entry => entry.Term.Text, StringComparer.CurrentCultureIgnoreCase)
    };
}
