namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>The raw text of one per-feature resource file.</summary>
/// <param name="Feature">The feature the file belongs to, taken from its file name; every key in it must start with <c>Feature.</c>.</param>
/// <param name="Json">A JSON object of key to text.</param>
internal sealed record LocalisationResource(string Feature, string Json);
