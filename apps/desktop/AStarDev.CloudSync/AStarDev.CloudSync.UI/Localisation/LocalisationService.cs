using System.Text.RegularExpressions;

namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>Looks texts up in per-language catalogues merged from the per-feature resource files.</summary>
/// <param name="culture">Formats template parameters.</param>
/// <param name="catalogues">Texts by language, then by key. A language absent here simply falls back to <see cref="DefaultLanguage"/>.</param>
/// <param name="language">The active language.</param>
internal sealed partial class LocalisationService(ICultureService culture, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogues, string language) : ILocalisationService
{
    /// <summary>The language every key must exist in, and the fallback for the rest.</summary>
    public const string DefaultLanguage = "en-GB";

    /// <inheritdoc/>
    public string Language { get; } = language;

    /// <inheritdoc/>
    public string Get(string key) => Find(Language, key) ?? Find(DefaultLanguage, key) ?? $"!{key}!";

    /// <inheritdoc/>
    public string Format(string key, params (string Name, object? Value)[] parameters) =>
        Placeholder().Replace(Get(key), match => parameters.Any(p => p.Name == match.Groups[1].Value) ? culture.FormatValue(parameters.First(p => p.Name == match.Groups[1].Value).Value) : match.Value);

    private string? Find(string catalogueLanguage, string key) => catalogues.TryGetValue(catalogueLanguage, out var catalogue) && catalogue.TryGetValue(key, out var text) ? text : null;

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
