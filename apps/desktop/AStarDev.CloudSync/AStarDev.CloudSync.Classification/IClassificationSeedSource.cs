using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Supplies the shipped seed.</summary>
internal interface IClassificationSeedSource
{
    /// <summary>Loads the seed.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The seed, or <see cref="InvalidSeed"/> when it is missing or malformed.</returns>
    Task<Result<ClassificationSeed, InvalidSeed>> LoadAsync(CancellationToken cancellationToken);
}
