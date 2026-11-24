using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification;

/// <summary>Registers the file classification engine.</summary>
public static class ClassificationServiceCollectionExtensions
{
    private const string SeedResourcePrefix = "AStarDev.CloudSync.Classification.Seed.";

    /// <summary>
    /// Registers the shipped seed and its seeder, the vocabulary provider and <see cref="IClassificationStartup"/>. The stores come from Persistence, which must be registered too.
    /// Nothing touches the database until <see cref="IClassificationStartup.RunAsync"/> runs.
    /// </summary>
    /// <param name="services">The collection to add the classification services to.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCloudSyncPersistence().AddClassification();
    /// await provider.GetRequiredService&lt;IClassificationStartup&gt;().RunAsync(cancellationToken);
    /// </code>
    /// </example>
    public static IServiceCollection AddClassification(this IServiceCollection services)
    {
        services.AddSingleton<IClassificationSeedSource>(_ => new EmbeddedClassificationSeedSource(typeof(ClassificationServiceCollectionExtensions).Assembly, SeedResourcePrefix));
        services.AddSingleton<IClassificationVocabularyProvider, ClassificationVocabularyProvider>();
        services.AddSingleton<ClassificationSeeder>();
        services.AddSingleton<IClassificationStartup, ClassificationStartup>();

        return services;
    }
}
