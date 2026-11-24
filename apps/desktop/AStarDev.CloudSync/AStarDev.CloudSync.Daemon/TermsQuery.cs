using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetClassificationTerms"/> from the category, term and link stores.</summary>
internal sealed class TermsQuery(ICategoryStore categories, ITermStore terms, IFileClassificationStore links)
{
    /// <summary>Reads the categories, the terms and how many files each term has.</summary>
    /// <returns>The vocabulary, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ClassificationTermsView, IpcError>> HandleAsync(GetClassificationTerms query, CancellationToken cancellationToken)
    {
        var storedCategories = await categories.ListAsync(cancellationToken);
        var storedTerms = await terms.ListAsync(cancellationToken);
        var counts = await links.GetFileCountsAsync(cancellationToken);

        return (storedCategories, storedTerms, counts) switch
        {
            (Ok<IReadOnlyList<Category>> categoryList, Ok<IReadOnlyList<Term>> termList, Ok<IReadOnlyDictionary<TermId, int>> countList) => new Ok<ClassificationTermsView>(View(categoryList.Value, termList.Value, countList.Value)),
            (Fail<ClassificationError> fail, _, _) => Failed(fail.Error),
            (_, Fail<ClassificationError> fail, _) => Failed(fail.Error),
            (_, _, Fail<ClassificationError> fail) => Failed(fail.Error),
            _ => Failed(new StorageFailed("The vocabulary could not be read"))
        };
    }

    private static Fail<IpcError> Failed(ClassificationError error) => new(new HandlerFailed(ClassificationErrorMessages.Describe(error, "read", "classification terms")));

    private static ClassificationTermsView View(IReadOnlyList<Category> categories, IReadOnlyList<Term> terms, IReadOnlyDictionary<TermId, int> counts) =>
        new([.. categories.Select(category => new ClassificationCategoryView(category.Id.Value, category.Name))], [.. terms.Select(term => new ClassificationTermView(term.Id.Value, term.Text, term.CategoryId.Value, term.Source.ToString(), term.Status == TermStatus.Ignored, counts.GetValueOrDefault(term.Id)))]);
}
