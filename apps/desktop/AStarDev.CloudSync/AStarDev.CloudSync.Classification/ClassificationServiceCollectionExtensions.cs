using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification;

/// <summary>Registers the file classification engine.</summary>
public static class ClassificationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the classification services. Registers nothing yet; the engine, vocabulary and startup steps are added here as they land.
    /// Safe to call twice.
    /// </summary>
    /// <param name="services">The collection to add the classification services to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddClassification(this IServiceCollection services) => services;
}
