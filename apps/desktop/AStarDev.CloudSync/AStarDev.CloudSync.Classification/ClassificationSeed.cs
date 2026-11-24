namespace AStarDev.CloudSync.Classification;

/// <summary>The shipped seed data: categories and terms, with a version so a changed seed can be detected.</summary>
/// <param name="Version">The seed version; the versions of the files that make it up joined with a plus sign.</param>
/// <param name="Categories">The category names.</param>
/// <param name="Terms">The terms. Every term names a category in <paramref name="Categories"/>.</param>
internal sealed record ClassificationSeed(string Version, IReadOnlyList<string> Categories, IReadOnlyList<SeedTerm> Terms);
