using System.Reflection;
using System.Text.Json;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Loads every embedded seed JSON file under a resource prefix and merges them into one seed. The versions of the files are joined with a plus sign.</summary>
/// <param name="assembly">The assembly holding the embedded seed files.</param>
/// <param name="resourcePrefix">The manifest resource name prefix that selects the seed files.</param>
internal sealed class EmbeddedClassificationSeedSource(Assembly assembly, string resourcePrefix) : IClassificationSeedSource
{
    /// <inheritdoc />
    public Task<Result<ClassificationSeed, InvalidSeed>> LoadAsync(CancellationToken cancellationToken)
        => Task.FromResult(Try.Run(Read).ToResult(exception => new InvalidSeed(exception.GetType().Name)));

    private static JsonElement[] Items(JsonElement root, string property) => root.TryGetProperty(property, out var items) ? [.. items.EnumerateArray()] : [];

    private static SeedTerm ToTerm(JsonElement term) => new(term.GetProperty("text").GetString() ?? string.Empty, term.GetProperty("category").GetString() ?? string.Empty);

    private static string VersionOf(JsonElement root) => root.TryGetProperty("version", out var version) ? version.GetString() ?? string.Empty : string.Empty;

    private ClassificationSeed Read()
    {
        var names = assembly.GetManifestResourceNames().Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).ToList();
        if (names.Count == 0) throw new InvalidOperationException("No seed files.");

        var roots = names.Select(Parse).ToList();
        var versions = roots.Select(VersionOf).Where(version => version.Length > 0).Distinct();
        var categories = roots.SelectMany(root => Items(root, "categories")).Select(category => category.GetProperty("name").GetString() ?? string.Empty);

        return new ClassificationSeed(string.Join('+', versions), [.. categories], [.. roots.SelectMany(root => Items(root, "terms")).Select(ToTerm)]);
    }

    private JsonElement Parse(string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException("Seed file missing.");
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.Clone();
    }
}
