namespace AStarDev.CloudSync.Classification;

/// <summary>Applies the shipped seed at startup.</summary>
/// <param name="seeder">Writes the seed when it is new.</param>
internal sealed class ClassificationStartup(ClassificationSeeder seeder) : IClassificationStartup
{
    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken) => _ = await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
}
