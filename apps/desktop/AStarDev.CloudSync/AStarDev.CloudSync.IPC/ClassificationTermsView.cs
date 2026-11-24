namespace AStarDev.CloudSync.IPC;

/// <summary>The classification vocabulary, answering <see cref="GetClassificationTerms"/>.</summary>
/// <param name="Categories">Every category, ordered by name ignoring case.</param>
/// <param name="Terms">Every term, ignored ones included so the user can bring them back, ordered by text.</param>
public sealed record ClassificationTermsView(IReadOnlyList<ClassificationCategoryView> Categories, IReadOnlyList<ClassificationTermView> Terms);
