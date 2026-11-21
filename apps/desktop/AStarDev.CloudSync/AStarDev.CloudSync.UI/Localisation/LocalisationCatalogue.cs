using System.Reflection;
using System.Text.Json;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>Merges the per-feature resource files of one language into a single key to text map.</summary>
internal static class LocalisationCatalogue
{
    private const string FolderMarker = ".Localisation.Strings.";

    /// <summary>
    /// Reads every embedded <c>Localisation/Strings/&lt;Feature&gt;.&lt;language&gt;.json</c> of <paramref name="assembly"/> for <paramref name="language"/>, so a new feature adds a file and edits nothing shared.
    /// </summary>
    /// <returns>The merged texts; an empty map when no file exists for the language; or the first <see cref="LocalisationError"/> found.</returns>
    public static Result<IReadOnlyDictionary<string, string>, LocalisationError> Load(Assembly assembly, string language)
    {
        var suffix = $".{language}.json";
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(FolderMarker, StringComparison.Ordinal) && name.EndsWith(suffix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => new LocalisationResource(FeatureOf(name, suffix), ReadText(assembly, name)))
            .ToList();

        return Merge(resources);
    }

    /// <summary>Merges the resources in order.</summary>
    /// <returns>The merged texts; <see cref="MalformedResource"/> for invalid JSON, <see cref="ForeignKey"/> for a key outside its feature, <see cref="DuplicateKey"/> for a key seen twice.</returns>
    public static Result<IReadOnlyDictionary<string, string>, LocalisationError> Merge(IEnumerable<LocalisationResource> resources)
    {
        var merged = new Dictionary<string, string>();
        foreach (var resource in resources)
        {
            var parsed = Try.Run(() => JsonSerializer.Deserialize<Dictionary<string, string>>(resource.Json) ?? [])
                .ToResult(exception => (LocalisationError)new MalformedResource(resource.Feature, exception.Message));
            if (parsed.Value is not Ok<Dictionary<string, string>> ok) return new Fail<LocalisationError>(((Fail<LocalisationError>)parsed.Value!).Error);

            foreach (var (key, text) in ok.Value)
            {
                if (!key.StartsWith($"{resource.Feature}.", StringComparison.Ordinal)) return new Fail<LocalisationError>(new ForeignKey(resource.Feature, key));
                if (!merged.TryAdd(key, text)) return new Fail<LocalisationError>(new DuplicateKey(key));
            }
        }

        return new Ok<IReadOnlyDictionary<string, string>>(merged);
    }

    private static string FeatureOf(string resourceName, string suffix)
    {
        var start = resourceName.IndexOf(FolderMarker, StringComparison.Ordinal) + FolderMarker.Length;

        return resourceName[start..^suffix.Length];
    }

    private static string ReadText(Assembly assembly, string resourceName)
    {
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName)!);

        return reader.ReadToEnd();
    }
}
