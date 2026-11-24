using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AStarDev.CloudSync.Classification;

/// <summary>Registers the file classification engine.</summary>
public static class ClassificationServiceCollectionExtensions
{
    private const string SeedResourcePrefix = "AStarDev.CloudSync.Classification.Seed.";
    private const string ListResourcePrefix = "AStarDev.CloudSync.Classification.Lists.";

    /// <summary>
    /// Registers the shipped seed and its seeder, the vocabulary provider, <see cref="IClassificationSuggestions"/>, <see cref="IClassificationReclassifier"/> and <see cref="IClassificationStartup"/>. The stores come from Persistence, which must be registered first: the item index it registers is wrapped so every crawled or changed file is classified.
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
        services.AddSingleton<IClassificationSignal, ExtensionSignal>();
        services.AddSingleton<IClassificationSignal, FolderSegmentSignal>();
        services.AddSingleton<IClassificationSignal, DictionaryTermSignal>();
        services.AddSingleton<FileClassifier>();
        services.AddSingleton(_ => new EmbeddedWordLists(typeof(ClassificationServiceCollectionExtensions).Assembly, ListResourcePrefix));
        services.AddSingleton<IKnownWordList>(provider => provider.GetRequiredService<EmbeddedWordLists>());
        services.AddSingleton<IFirstNameList>(provider => provider.GetRequiredService<EmbeddedWordLists>());
        services.AddSingleton<IClassificationSuggestions, ClassificationSuggestions>();
        services.AddSingleton<ClassificationSeeder>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(ReclassifyOptions.Default);
        services.AddSingleton<IClassificationReclassifier, ClassificationReclassifier>();
        services.AddSingleton<IClassificationStartup, ClassificationStartup>();

        DecorateItemIndexStore(services);

        return services;
    }

    private static void DecorateItemIndexStore(IServiceCollection services)
    {
        var original = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IItemIndexStore));
        if (original is null) return;

        services.Remove(original);
        services.Add(ServiceDescriptor.Describe(typeof(IItemIndexStore), provider => ActivatorUtilities.CreateInstance<ClassifyingItemIndexStore>(provider, Inner(provider, original)), original.Lifetime));
    }

    private static IItemIndexStore Inner(IServiceProvider provider, ServiceDescriptor original) =>
        (IItemIndexStore)(original.ImplementationInstance ?? original.ImplementationFactory?.Invoke(provider) ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!));
}
