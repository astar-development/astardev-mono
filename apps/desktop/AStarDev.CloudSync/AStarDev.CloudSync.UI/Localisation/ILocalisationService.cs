namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>Supplies every user-visible text, so no view or view model holds a literal.</summary>
internal interface ILocalisationService
{
    /// <summary>The language of the texts, for example <c>en-GB</c>.</summary>
    public string Language { get; }

    /// <summary>The text for <paramref name="key"/>. A key missing from <see cref="Language"/> falls back to the default language; a key missing from both comes back as <c>!key!</c> so the gap is visible but never throws.</summary>
    public string Get(string key);

    /// <summary>
    /// The text for <paramref name="key"/> with each <c>{name}</c> placeholder replaced by the matching parameter, formatted by the <see cref="ICultureService"/>.
    /// A placeholder with no parameter stays as written. Never concatenate texts in code; give the template the placeholders instead.
    /// </summary>
    /// <example>
    /// <code>
    /// var text = localisation.Format("Common.ItemCount", ("count", 1234));
    /// </code>
    /// </example>
    public string Format(string key, params (string Name, object? Value)[] parameters);
}
