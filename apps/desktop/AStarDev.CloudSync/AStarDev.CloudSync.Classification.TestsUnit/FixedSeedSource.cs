using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification.TestsUnit;

internal sealed class FixedSeedSource(Result<ClassificationSeed, InvalidSeed> result) : IClassificationSeedSource
{
    public Task<Result<ClassificationSeed, InvalidSeed>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(result);

    public static ClassificationSeed Seed(string version, params SeedTerm[] terms) => new(version, ["Colour", "Place"], terms);
}
