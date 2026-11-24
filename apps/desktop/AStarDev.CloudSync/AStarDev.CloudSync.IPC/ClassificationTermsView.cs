namespace AStarDev.CloudSync.IPC;

/// <summary>The classification vocabulary, answering <see cref="GetClassificationTerms"/>.</summary>
/// <param name="Categories">Every category, ordered by name ignoring case, in every reply.</param>
/// <param name="Terms">The requested page of terms, ignored ones included so the user can bring them back.</param>
/// <param name="Total">How many terms match the filter in all, so a caller can page. At least <c>Terms.Count</c>.</param>
public sealed record ClassificationTermsView(IReadOnlyList<ClassificationCategoryView> Categories, IReadOnlyList<ClassificationTermView> Terms, int Total);
